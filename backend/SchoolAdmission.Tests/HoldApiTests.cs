using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.Models;
using Xunit;

public class HoldApiTests
{
    private static string Student(int n) => "20152691" + n.ToString("D9");

    private static async Task<string> CreateClassAsync(HttpClient client, int seats)
    {
        var response = await client.PostAsJsonAsync("/classes",
            new CreateClassRequest("Class 6 - Test " + Guid.NewGuid(), seats));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedClassResponse>())!.Id;
    }

    private static Task<HttpResponseMessage> ApplyAsync(
        HttpClient client, string classId, string studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Holding_a_seat_reduces_available_seats()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var classId = await CreateClassAsync(client, 5);

        var apply = await ApplyAsync(client, classId, Student(1));
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var cls = await client.GetFromJsonAsync<ClassResponse>($"/classes/{classId}");
        Assert.Equal(1, cls!.HeldSeats);
        Assert.Equal(4, cls.AvailableSeats);
    }

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

    [Fact]
    public async Task Concurrent_applications_never_exceed_total_seats()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var classId = await CreateClassAsync(client, 10);

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 60)
                .Select(i => ApplyAsync(client, classId, Student(100 + i))));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(50, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await client.GetFromJsonAsync<ClassResponse>($"/classes/{classId}");
        Assert.True(cls!.HeldSeats + cls.ConfirmedSeats <= cls.TotalSeats);
        Assert.Equal(0, cls.AvailableSeats);
    }

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
}
