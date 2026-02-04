using Pocketree.Api.Tests.Helpers;

namespace Pocketree.Api.Tests.Integration;

public class UserApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(Skip = "Integration tests - TestWebApplicationFactory has MySQL+InMemory provider conflict")]
    public async System.Threading.Tasks.Task Login_Should_ReturnUserWithNewColumns()
    {
        // Arrange
        var loginRequest = new { Username = "testuser", Password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/LoginApi", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.User!.UserRole.Should().Be("Player");
        result.User.IsOnline.Should().BeFalse();
    }

    [Fact(Skip = "Integration tests - TestWebApplicationFactory has MySQL+InMemory provider conflict")]
    public async System.Threading.Tasks.Task Register_Should_CreateUserWithDefaultValues()
    {
        // Arrange
        var registerRequest = new
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "SecurePass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/RegisterApi", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Login to verify
        var loginResponse = await _client.PostAsJsonAsync("/api/User/LoginApi", 
            new { Username = "newuser", Password = "SecurePass123!" });
        
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.User.Should().NotBeNull();
        loginResult!.User!.UserRole.Should().Be("Player");
        loginResult.User.IsOnline.Should().BeFalse();
    }
}

public record LoginResponse(string Token, UserDto? User);
public record UserDto(string Username, string UserRole, bool IsOnline, string Email);