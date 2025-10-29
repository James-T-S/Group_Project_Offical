using Group_Project_Offical.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// Sessions + HttpContext accessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".SustainWear.Session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register your SessionService (concrete type since your PageModel asks for it)
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

// IMPORTANT: enable session before mapping Razor Pages
app.UseSession();

app.UseAuthentication();   // if you’re using it
app.UseAuthorization();

app.MapRazorPages();

app.Run();
