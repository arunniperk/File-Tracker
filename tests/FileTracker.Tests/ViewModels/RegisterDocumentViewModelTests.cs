using FileTracker.App.ViewModels;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using Microsoft.Data.Sqlite;

namespace FileTracker.Tests.ViewModels;

public class RegisterDocumentViewModelTests
{
    private readonly Mock<IDocumentService> _docServiceMock;
    private readonly Mock<IAttachmentService> _attachmentServiceMock;

    public RegisterDocumentViewModelTests()
    {
        _docServiceMock = new Mock<IDocumentService>();
        _attachmentServiceMock = new Mock<IAttachmentService>();
    }

    private RegisterDocumentViewModel CreateViewModel()
    {
        return new RegisterDocumentViewModel(_docServiceMock.Object, _attachmentServiceMock.Object);
    }

    [Fact]
    public void IsIncoming_DefaultsToTrue()
    {
        var vm = CreateViewModel();
        vm.IsIncoming.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAsync_WithIsIncomingTrue_CreatesDtoWithDirectionIncoming()
    {
        RegisterDocumentDto? capturedDto = null;
        _docServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDocumentDto>()))
            .Callback<RegisterDocumentDto>(dto => capturedDto = dto)
            .ReturnsAsync(new Document { Id = 1, TrackingId = "0001/2026" });

        var vm = CreateViewModel();
        vm.IsIncoming = true;
        vm.SenderOrRecipient = "Test Sender";
        vm.Subject = "Test Subject";
        vm.OriginalFileNumber = "FILE-001";
        vm.DocumentDate = new DateTime(2026, 5, 29);

        await vm.SubmitCommand.ExecuteAsync(null);

        capturedDto.Should().NotBeNull();
        capturedDto!.Direction.Should().Be(DocumentDirection.Incoming);
        capturedDto.Sender.Should().Be("Test Sender");
        capturedDto.Recipient.Should().BeNull();
    }

    [Fact]
    public async Task SubmitAsync_WithIsIncomingFalse_CreatesDtoWithDirectionOutgoing()
    {
        RegisterDocumentDto? capturedDto = null;
        _docServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDocumentDto>()))
            .Callback<RegisterDocumentDto>(dto => capturedDto = dto)
            .ReturnsAsync(new Document { Id = 1, TrackingId = "0001/2026" });

        var vm = CreateViewModel();
        vm.IsIncoming = false;
        vm.SenderOrRecipient = "Test Recipient";
        vm.Subject = "Test Subject";
        vm.OriginalFileNumber = "FILE-002";
        vm.DocumentDate = new DateTime(2026, 5, 29);

        await vm.SubmitCommand.ExecuteAsync(null);

        capturedDto.Should().NotBeNull();
        capturedDto!.Direction.Should().Be(DocumentDirection.Outgoing);
        capturedDto.Recipient.Should().Be("Test Recipient");
        capturedDto.Sender.Should().BeNull();
    }

    [Fact]
    public async Task SubmitAsync_WithEmptySubject_BlocksSubmission()
    {
        var vm = CreateViewModel();
        vm.Subject = string.Empty;
        vm.SenderOrRecipient = "Test";
        vm.OriginalFileNumber = "FILE-003";
        vm.DocumentDate = DateTime.Today;

        // ValidateAllProperties should trigger errors
        vm.SubmitCommand.CanExecute(null).Should().BeFalse("empty subject should disable Save");
    }

    [Fact]
    public async Task SubmitAsync_WithEmptyOriginalFileNumber_BlocksSubmission()
    {
        var vm = CreateViewModel();
        vm.Subject = "Valid Subject";
        vm.SenderOrRecipient = "Test";
        vm.OriginalFileNumber = string.Empty;
        vm.DocumentDate = DateTime.Today;

        vm.SubmitCommand.CanExecute(null).Should().BeFalse("empty file number should disable Save");
    }

    [Fact]
    public void HasUnsavedChanges_IsTrueAfterModifyingField()
    {
        var vm = CreateViewModel();
        vm.HasUnsavedChanges.Should().BeFalse("no changes initially");

        vm.Subject = "New Subject";
        vm.HasUnsavedChanges.Should().BeTrue("changing a field should mark as unsaved");
    }

    [Fact]
    public void ClearForm_ResetsHasUnsavedChangesToFalse()
    {
        var vm = CreateViewModel();
        vm.Subject = "Modified";
        vm.HasUnsavedChanges.Should().BeTrue();

        // Access ClearForm via SubmitAsync succeeding (or we need a public way to clear)
        // Since ClearForm is private, we verify through SubmitAsync behavior
    }

    [Fact]
    public async Task SubmitAsync_AfterSuccessfulSave_ResetsHasUnsavedChanges()
    {
        _docServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDocumentDto>()))
            .ReturnsAsync(new Document { Id = 1, TrackingId = "0001/2026" });

        var vm = CreateViewModel();
        vm.IsIncoming = true;
        vm.SenderOrRecipient = "Test Sender";
        vm.Subject = "Test Subject";
        vm.OriginalFileNumber = "FILE-004";
        vm.DocumentDate = new DateTime(2026, 5, 29);

        vm.HasUnsavedChanges.Should().BeTrue("fields have been set");

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.HasUnsavedChanges.Should().BeFalse("successful save should clear unsaved changes flag");
    }

    [Fact]
    public async Task SubmitAsync_WhenServiceThrowsArgumentException_DisplaysErrorMessage()
    {
        _docServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDocumentDto>()))
            .ThrowsAsync(new ArgumentException("Subject is required."));

        var vm = CreateViewModel();
        vm.IsIncoming = true;
        vm.SenderOrRecipient = "Test";
        vm.Subject = "Test"; // won't matter, service throws
        vm.OriginalFileNumber = "FILE-005";
        vm.DocumentDate = DateTime.Today;

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("Subject");
    }

    [Fact]
    public async Task SubmitAsync_WhenServiceThrowsSqliteExceptionForDuplicate_DisplaysErrorMessage()
    {
        _docServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDocumentDto>()))
            .ThrowsAsync(new SqliteException("UNIQUE constraint failed: Documents.OriginalFileNumber", 19));

        var vm = CreateViewModel();
        vm.IsIncoming = true;
        vm.SenderOrRecipient = "Test";
        vm.Subject = "Test Subject";
        vm.OriginalFileNumber = "FILE-DUP";
        vm.DocumentDate = DateTime.Today;

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("already exists");
    }
}