using BlazorServerCaptureUser.Areas.Identity;
using BlazorServerCaptureUser.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddSingleton<WeatherForecastService>();

builder.Services.AddScoped<UserService>();
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, UserCircuitHandler>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public class UserService
{
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    public ClaimsPrincipal GetUser()
    {
        return _currentUser;
    }

    internal void SetUser(ClaimsPrincipal user)
    {
        // This might be called more than once
        if (_currentUser != user)
        {
            _currentUser = user;
        }
    }
}

internal sealed class UserCircuitHandler : CircuitHandler, IDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly UserService _userService;

    public UserCircuitHandler(
        AuthenticationStateProvider authenticationStateProvider,
        UserService userService)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _userService = userService;
    }

    public void Dispose()
    {
        _authenticationStateProvider.AuthenticationStateChanged -= AuthenticationChanged;
    }

    // This and the method below are only needed if you want to update the user after the app has re-connected.
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // If you want to update the service, then you can attach an event handler as follows
        _authenticationStateProvider.AuthenticationStateChanged += AuthenticationChanged;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    private void AuthenticationChanged(Task<AuthenticationState> task)
    {
        _ = UpdateAuthentication(task);

        async Task UpdateAuthentication(Task<AuthenticationState> task)
        {
            try
            {
                var state = await task;
                _userService.SetUser(state.User);
            }
            catch
            {
                // Swallow all exceptions here since there is no way to report them
                // (and if they come from the task, they'll be reported somewhere else).
            }
        }
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // This gets called every time the circuit reconnects, setting the user for the lifetime of the connection.
        // Unless you implement updates.
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        _userService.SetUser(state.User);
    }
}