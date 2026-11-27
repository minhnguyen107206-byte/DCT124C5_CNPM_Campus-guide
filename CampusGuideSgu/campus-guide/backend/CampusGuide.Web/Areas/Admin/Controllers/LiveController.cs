using CampusGuide.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampusGuide.Web.Areas.Admin.Controllers;

/// <summary>Dashboard "Trực tiếp": số thiết bị đang truy cập / đang nghe thuyết minh, cập nhật mỗi 2 giây.</summary>
public class LiveController : AdminControllerBase
{
    private readonly PresenceService _presence;
    public LiveController(PresenceService presence) => _presence = presence;

    public IActionResult Index() => View(_presence.Snapshot());

    /// <summary>JSON cho trang dashboard tự cập nhật (số liệu tính ở PresenceService.Snapshot).</summary>
    [HttpGet]
    public IActionResult Data() => Json(_presence.Snapshot());
}
