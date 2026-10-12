using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AttachmentTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    ];

    private static readonly byte[] OggBytes =
    [
        0x4F, 0x67, 0x67, 0x53, 0x00, 0x02, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    private readonly ApiCollectionFixture _fixture;

    public AttachmentTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Participant_uploads_image_and_other_participant_downloads_it()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-sender@teamchat.test");
        var (otherId, tokenB) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-receiver@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        await using var connectionB = ApiTestHelper.CreateConnection(_fixture, tokenB);
        var received = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionB.On<MessageDto>("ReceiveMessage", m =>
        {
            if (m.ConversationId == conversationId)
            {
                received.TrySetResult(m);
            }
        });
        await connectionB.StartAsync();

        using var upload = await UploadAsync(client, tokenA, conversationId, PngBytes, "photo.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var message = await upload.Content.ReadFromJsonAsync<MessageDto>();

        Assert.NotNull(message);
        Assert.Equal(3, message!.Type);
        Assert.NotNull(message.Attachment);
        Assert.Equal("photo.png", message.Attachment!.FileName);
        Assert.Equal("image/png", message.Attachment.ContentType);

        var broadcast = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(message.Id, broadcast.Id);
        Assert.NotNull(broadcast.Attachment);

        var download = await DownloadAsync(client, tokenB, message.Attachment.Id);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(PngBytes, bytes);
    }

    [Fact]
    public async Task Forbidden_file_type_is_rejected()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-exe@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-exe-peer@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        byte[] executable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        using var upload = await UploadAsync(client, tokenA, conversationId, executable, "virus.exe", "application/octet-stream");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Too_large_file_is_rejected()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-big@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-big-peer@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var big = new byte[5000];
        Array.Copy(PngBytes, big, PngBytes.Length);

        using var upload = await UploadAsync(client, tokenA, conversationId, big, "big.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Non_participant_cannot_download_attachment()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-owner@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-peer@teamchat.test");
        var (_, tokenC) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "attach-stranger@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        using var upload = await UploadAsync(client, tokenA, conversationId, PngBytes, "secret.png", "image/png");
        upload.EnsureSuccessStatusCode();
        var message = await upload.Content.ReadFromJsonAsync<MessageDto>();

        var download = await DownloadAsync(client, tokenC, message!.Attachment!.Id);

        Assert.Equal(HttpStatusCode.Forbidden, download.StatusCode);
    }

    [Fact]
    public async Task Voice_message_is_stored_with_duration()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "voice-sender@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "voice-peer@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        using var upload = await UploadAsync(
            client, tokenA, conversationId, OggBytes, "vocal.ogg", "audio/ogg", isVoice: true, duration: 7);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var message = await upload.Content.ReadFromJsonAsync<MessageDto>();

        Assert.Equal(1, message!.Type);
        Assert.NotNull(message.Attachment);
        Assert.Equal("audio/ogg", message.Attachment!.ContentType);
        Assert.Equal(7, message.Attachment.DurationSec);
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string accessToken,
        Guid conversationId,
        byte[] bytes,
        string fileName,
        string contentType,
        bool isVoice = false,
        int? duration = null)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(isVoice ? "true" : "false"), "isVoice");
        if (duration is not null)
        {
            form.Add(new StringContent(duration.Value.ToString()), "durationSec");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversationId}/attachments")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> DownloadAsync(HttpClient client, string accessToken, Guid attachmentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/attachments/{attachmentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
