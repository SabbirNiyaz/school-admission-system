
using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.Models;
using Xunit;

public class HoldApiTests
{
    private static string Student(int n) => "20152691" + n.ToString("D9");

    private static async Task<string> CreateClassAsync(HttpClient client, int seats)
    {
        var response = await client.PostAsJsonAsync(
            "/classes",
            new CreateClassRequest("Class 6 - Test " + Guid.NewGuid(), seats));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedClassResponse>())!.Id;
    }

    private static Task<HttpResponseMessage> ApplyAsync(
        HttpClient client, string classId, string studentId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/classes/{classId}/holds");

        request.Headers.Add("X-Student-Id", studentId);

        return client.SendAsync(request);
    }

    private static async Task<HoldResponse> CreateHoldAsync(
        HttpClient client, string classId, string studentId)
    {
        var response = await ApplyAsync(client, classId, studentId);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<HoldResponse>())!;
    }

    private static Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client, string holdId, string studentId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/holds/{holdId}/confirm");

        request.Headers.Add("X-Student-Id", studentId);

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> CancelAsync(
        HttpClient client, string holdId, string studentId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete, $"/holds/{holdId}");

        request.Headers.Add("X-Student-Id", studentId);

        return client.SendAsync(request);
    }

    // TEST 1: Holding a seat reduces available seats.

    [Fact]
    public async Task Holding_a_seat_reduces_available_seats()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 5);

        var apply = await ApplyAsync(client, classId, Student(1));

        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var cls = await client.GetFromJsonAsync<ClassResponse>(
            $"/classes/{classId}");

        Assert.Equal(1, cls!.HeldSeats);
        Assert.Equal(4, cls.AvailableSeats);
    }

    // TEST 2: Full class rejects new and duplicate applications.

    [Fact]
    public async Task Full_class_rejects_new_and_duplicate_applications()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 1);

        Assert.Equal(HttpStatusCode.Created,
            (await ApplyAsync(client, classId, Student(1))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await ApplyAsync(client, classId, Student(2))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await ApplyAsync(client, classId, Student(1))).StatusCode);
    }

    // TEST 3: Concurrent applications cannot exceed total seats.

    [Fact]
    public async Task Concurrent_applications_never_exceed_total_seats()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 10);

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 60)
                .Select(i => ApplyAsync(client, classId, Student(100 + i))));

        Assert.Equal(10,
            responses.Count(r => r.StatusCode == HttpStatusCode.Created));

        Assert.Equal(50,
            responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await client.GetFromJsonAsync<ClassResponse>(
            $"/classes/{classId}");

        Assert.True(cls!.HeldSeats + cls.ConfirmedSeats <= cls.TotalSeats);
        Assert.Equal(0, cls.AvailableSeats);
    }

    // TEST 4: Expired hold releases the seat automatically.

    [Fact]
    public async Task Expired_hold_releases_the_seat_automatically()
    {
        using var factory = new ApiFactory { HoldSeconds = 2 };
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 1);

        Assert.Equal(HttpStatusCode.Created,
            (await ApplyAsync(client, classId, Student(1))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await ApplyAsync(client, classId, Student(2))).StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Equal(HttpStatusCode.Created,
            (await ApplyAsync(client, classId, Student(2))).StatusCode);
    }

    // TEST 5: A student can successfully confirm a hold.

    [Fact]
    public async Task Confirming_a_hold_changes_status_to_confirmed()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 2);
        var studentId = Student(1);

        var hold = await CreateHoldAsync(client, classId, studentId);

        var response = await ConfirmAsync(
            client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal("CONFIRMED", result!.Status);

        var cls = await client.GetFromJsonAsync<ClassResponse>(
            $"/classes/{classId}");

        Assert.Equal(0, cls!.HeldSeats);
        Assert.Equal(1, cls.ConfirmedSeats);
        Assert.Equal(1, cls.AvailableSeats);
    }

    // TEST 6: Confirming an already-confirmed hold is idempotent.

    [Fact]
    public async Task Confirming_an_already_confirmed_hold_is_idempotent()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 2);
        var studentId = Student(1);

        var hold = await CreateHoldAsync(client, classId, studentId);

        var first = await ConfirmAsync(
            client, hold.HoldId, studentId);

        var second = await ConfirmAsync(
            client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var cls = await client.GetFromJsonAsync<ClassResponse>(
            $"/classes/{classId}");

        Assert.Equal(1, cls!.ConfirmedSeats);
    }

    // TEST 7: Cancelling a hold releases its seat.

    [Fact]
    public async Task Cancelling_a_hold_releases_its_seat()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 1);
        var studentId = Student(1);

        var hold = await CreateHoldAsync(client, classId, studentId);

        var cancel = await CancelAsync(
            client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var nextStudent = await ApplyAsync(
            client, classId, Student(2));

        Assert.Equal(HttpStatusCode.Created, nextStudent.StatusCode);
    }

    // TEST 8: Invalid student ID is rejected.

    [Fact]
    public async Task Invalid_student_id_is_rejected()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 2);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/classes/{classId}/holds");

        request.Headers.Add("X-Student-Id", "12345");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // TEST 9: Another student cannot confirm someone else's hold.

    [Fact]
    public async Task Another_student_cannot_confirm_someone_elses_hold()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 2);

        var hold = await CreateHoldAsync(
            client, classId, Student(1));

        var response = await ConfirmAsync(
            client, hold.HoldId, Student(2));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // TEST 10: Another student cannot cancel someone else's hold.

    [Fact]
    public async Task Another_student_cannot_cancel_someone_elses_hold()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 2);

        var hold = await CreateHoldAsync(
            client, classId, Student(1));

        var response = await CancelAsync(
            client, hold.HoldId, Student(2));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // TEST 11: An expired hold cannot be confirmed.

    [Fact]
    public async Task Expired_hold_cannot_be_confirmed()
    {
        using var factory = new ApiFactory { HoldSeconds = 1 };
        var client = factory.CreateClient();

        var classId = await CreateClassAsync(client, 1);
        var studentId = Student(1);

        var hold = await CreateHoldAsync(
            client, classId, studentId);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        var response = await ConfirmAsync(
            client, hold.HoldId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
