using CampusGuide.Web.Data;
using CampusGuide.Web.Models;
using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Cho phép lưu DateTime không có múi giờ vào PostgreSQL (form datetime-local của trang Admin)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// Dùng dấu chấm thập phân cho tọa độ trên mọi máy (macOS/Windows cài tiếng Việt dùng dấu phẩy)
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- CSDL: SQLite (mặc định, không cần cài) hoặc PostgreSQL ----------
var provider = cfg["Database:Provider"] ?? "Sqlite";
var conn = cfg["Database:ConnectionString"] ?? "Data Source=campusguide.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)) o.UseNpgsql(conn);
    else o.UseSqlite(conn);
});

// ---------- MVC + xác thực cookie cho trang Admin ----------
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/account/login";
        o.AccessDeniedPath = "/admin/account/denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = false;
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("QuanTri", p => p.RequireRole(VaiTroTen.QuanTriVien));
    o.AddPolicy("BienTap", p => p.RequireRole(VaiTroTen.BienTapVien, VaiTroTen.QuanTriVien));
});
builder.Services.AddCors(o => o.AddPolicy("app", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------- Dịch vụ ----------
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();   // QrService lấy địa chỉ máy chủ cho mã QR
builder.Services.AddSingleton<PresenceService>();   // đếm thiết bị đang truy cập (dashboard Trực tiếp)
builder.Services.AddSingleton<IStorageService, LocalStorageService>();
// Dịch: MyMemory (miễn phí, không cần key). "Translation:Provider": "Demo" để dịch giả lập khi không có mạng.
if (string.Equals(cfg["Translation:Provider"], "Demo", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ITranslationService, DemoTranslationService>();
else
    builder.Services.AddScoped<ITranslationService, MyMemoryTranslationService>();
// Giọng đọc: giọng của thiết bị (app dùng flutter_tts, trang Admin dùng giọng của trình duyệt)
builder.Services.AddSingleton<ITtsService, DeviceTtsService>();
builder.Services.AddScoped<IPushService, FirebasePushService>();
builder.Services.AddScoped<GlossaryService>();
builder.Services.AddScoped<TranslationPipeline>();
builder.Services.AddScoped<ContentResolver>();
builder.Services.AddScoped<QrService>();
builder.Services.AddHostedService<PublicTunnelService>();
builder.Services.AddScoped<PackageService>();
builder.Services.AddScoped<StatsService>();
// Khu người dùng (trang web công khai)
builder.Services.AddScoped<SiteContext>();
builder.Services.AddScoped<SiteContentService>();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
// Xuất chữ tiếng Việt / Nhật / Hàn... nguyên dạng thay vì &#x...; (trang nhẹ hơn, vẫn mã hoá < > & ")
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));   // nén gzip/brotli cho trang tải nhanh trên 4G

var app = builder.Build();

// ---------- Tạo CSDL + dữ liệu mẫu lần đầu chạy ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await SchemaUpdater.RunAsync(db);   // thêm bảng mới (LuotTruyCap) vào CSDL đã có sẵn, không mất dữ liệu
    await SeedData.RunAsync(db);
    var (tbMau, videoMau) = await SeedData.RunSamplesAsync(db);   // thông báo + video mẫu: chỉ thêm mục chưa có
    Console.WriteLine($"[Dữ liệu mẫu] Đã thêm {tbMau} thông báo, {videoMau} video.");
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/admin/home/error");

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    // CSS/JS/ảnh của khu người dùng có ?v= (asp-append-version) nên cho trình duyệt lưu đệm 7 ngày
    OnPrepareResponse = c =>
    {
        if (c.Context.Request.Path.StartsWithSegments("/site"))
            c.Context.Response.Headers.CacheControl = "public,max-age=604800";
    },
});
app.UseRouting();
app.UseCors("app");
app.UseAuthentication();
app.UseAuthorization();

// "/" giờ là Trang chủ khu người dùng (Controllers/Site); trang quản trị vẫn ở /admin
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
