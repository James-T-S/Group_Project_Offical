using Group_Project_Offical.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".SustainWear.Session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<SessionService>();

var app = builder.Build();

// Usual middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
