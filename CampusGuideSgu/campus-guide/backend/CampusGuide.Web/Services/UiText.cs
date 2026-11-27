namespace CampusGuide.Web.Services;

/// <summary>
/// Chuỗi giao diện (menu, nút, thông báo) của khu người dùng theo ngôn ngữ.
/// Nội dung thật (điểm, thông báo, thuật ngữ) lấy từ bản dịch đã duyệt ở Admin; phần chữ trên nút/menu thì nằm ở đây.
/// Ngôn ngữ chưa có bảng chuỗi (VD fr thêm ở Admin) → dùng tiếng Anh, thiếu nữa thì tiếng Việt.
/// </summary>
public static class UiText
{
    public static string Get(string lang, string key)
    {
        if (All.TryGetValue(lang, out var d) && d.TryGetValue(key, out var s)) return s;
        if (lang != "vi" && En.TryGetValue(key, out var e)) return e;
        return Vi.TryGetValue(key, out var v) ? v : key;
    }

    private static readonly Dictionary<string, string> Vi = new()
    {
        ["meta_desc"] = "Hướng dẫn tham quan, bản đồ và tra cứu khuôn viên Trường Đại học Sài Gòn",
        ["skip"] = "Bỏ qua tới nội dung", ["sgu_short"] = "Đại học Sài Gòn", ["sgu_full"] = "Trường Đại học Sài Gòn (SGU)",
        ["nav_home"] = "Trang chủ", ["nav_points"] = "Điểm tham quan", ["nav_map"] = "Bản đồ", ["nav_tours"] = "Tour",
        ["nav_notices"] = "Thông báo", ["nav_live"] = "Trực tiếp", ["nav_glossary"] = "Thuật ngữ", 
        ["scan_qr"] = "Quét QR", ["language"] = "Ngôn ngữ", 
        ["menu"] = "Menu", ["more"] = "Thêm", ["close"] = "Đóng", ["about"] = "Giới thiệu", ["explore"] = "Khám phá", ["info"] = "Thông tin",
        ["footer_about"] = "CampusGuide giúp tân sinh viên, khách tham quan và cựu sinh viên tìm đường, nghe thuyết minh và khám phá khuôn viên SGU bằng nhiều ngôn ngữ.",
        ["hero_eyebrow"] = "Hướng dẫn tham quan thông minh",
        ["hero_title"] = "Khám phá Trường Đại học Sài Gòn",
        ["hero_lead"] = "Bản đồ khuôn viên, thuyết minh đa ngôn ngữ, tour tham quan và quét mã QR ngay tại từng toà nhà.",
        ["start_tour"] = "Bắt đầu tour", ["open_map"] = "Mở bản đồ",
        ["stat_points"] = "điểm tham quan", ["stat_tours"] = "tour", ["stat_langs"] = "ngôn ngữ",
        ["featured_points"] = "Điểm nổi bật", ["hot_tours"] = "Tour được yêu thích", ["latest_notices"] = "Thông báo mới",
        ["see_all"] = "Xem tất cả", ["no_points"] = "Chưa có điểm nào được xuất bản.", ["no_tours"] = "Chưa có tour nào.",
        ["no_notices"] = "Hiện chưa có thông báo nào.", ["urgent"] = "Khẩn", ["happening"] = "Đang diễn ra", ["upcoming"] = "Sắp diễn ra",
        ["visitors_now"] = "Khách đang tham quan", ["live_hint"] = "Xem số khách đang tham quan và các buổi phát trực tiếp →",
        ["cta_title"] = "Đang đứng trước một toà nhà?", ["cta_text"] = "Quét mã QR dán tại toà nhà, phòng ban để nghe thuyết minh ngay bằng ngôn ngữ của bạn – không cần cài app, không cần đăng nhập.",
        ["tour_badge"] = "Tour tham quan", ["stops"] = "điểm dừng", ["min"] = "phút",
        ["points_lead"] = "Toà nhà, phòng học, hội trường, thư viện, căn tin… trong khuôn viên trường.",
        ["search_points"] = "Tìm toà nhà, phòng, khoa… (không cần dấu)", ["all"] = "Tất cả", ["found_n"] = "{0} điểm",
        ["view_on_map"] = "Xem trên bản đồ", ["no_results"] = "Không tìm thấy kết quả phù hợp.", 
        ["back"] = "Quay lại", ["visits"] = "lượt", ["reviews"] = "đánh giá", 
        ["fallback_note"] = "Chưa có bản dịch cho ngôn ngữ này – đang hiển thị ngôn ngữ dự phòng.",
        ["machine_note"] = "Bản dịch tự động, đang chờ biên tập viên duyệt.",
        ["listen"] = "Nghe thuyết minh", ["stop"] = "Dừng", ["tts_unsupported"] = "Trình duyệt không hỗ trợ đọc", ["no_text"] = "Chưa có nội dung thuyết minh.",
        ["videos"] = "Video", ["rate_title"] = "Đánh giá điểm này", ["comment_ph"] = "Cảm nhận của bạn (không bắt buộc)", ["send"] = "Gửi",
        ["rate_thanks"] = "Cảm ơn bạn đã đánh giá!",
        ["directions"] = "Chỉ đường", ["share"] = "Chia sẻ", ["qr_title"] = "Mã QR tại chỗ",
        ["qr_text"] = "Mã này được dán tại điểm. Quét bằng camera điện thoại để nghe thuyết minh.",
        ["in_tours"] = "Thuộc tour", ["nearby"] = "Điểm gần đây",
        ["qr_invalid"] = "Mã QR không hợp lệ. Hãy quét mã dán tại các toà nhà của trường.",
        ["map_layer"] = "Đổi lớp bản đồ", ["my_location"] = "Vị trí của tôi", ["details"] = "Chi tiết", ["you_here"] = "Bạn ở đây",
        ["geo_denied"] = "Không lấy được vị trí – hãy cho phép quyền vị trí trong trình duyệt.",
        ["geo_insecure"] = "Trình duyệt chỉ cho lấy vị trí khi trang mở bằng https.",
        ["scan_lead"] = "Hướng camera vào mã QR dán tại toà nhà, phòng ban để nghe thuyết minh.",
        ["open_camera"] = "Mở camera", ["scan_from_image"] = "Quét từ ảnh", ["enter_code"] = "Hoặc nhập mã in dưới QR", ["go"] = "Mở",
        ["cam_insecure"] = "Camera chỉ hoạt động khi trang mở bằng https. Hãy dùng camera của điện thoại để quét, hoặc nhập mã bên dưới.",
        ["cam_denied"] = "Không mở được camera. Hãy cho phép quyền camera hoặc dùng \"Quét từ ảnh\".",
        ["site_qr_title"] = "Mã QR vào CampusGuide", ["site_qr_lead"] = "Quét mã này bằng camera điện thoại để mở CampusGuide ngay – không cần cài app.", ["scan_hint"] = "Mở camera điện thoại và hướng vào mã", ["scan_points_hint"] = "Tại mỗi toà nhà, phòng ban cũng có mã QR riêng – quét để nghe thuyết minh của điểm đó.",
        ["tours_lead"] = "Các lộ trình tham quan khuôn viên dành cho khách, tân sinh viên và cựu sinh viên.",
        ["aud_visitor"] = "Khách tham quan", ["aud_freshman"] = "Tân sinh viên", ["aud_alumni"] = "Cựu sinh viên",
        ["duration"] = "thời lượng", ["distance"] = "quãng đường", 
        ["itinerary"] = "Lịch trình",
        ["notices_lead"] = "Thông báo quan trọng và sự kiện sắp diễn ra trong khuôn viên.",
        ["upcoming_events"] = "Sự kiện sắp diễn ra", ["no_upcoming"] = "Chưa có sự kiện sắp tới.",
        ["live_lead"] = "Video giới thiệu trường và các buổi phát trực tiếp sự kiện.",
        ["listening_now"] = "Đang nghe thuyết minh", ["busy_points"] = "Điểm đông khách", ["live_now"] = "Đang phát trực tiếp",
        ["intro_videos"] = "Video giới thiệu", ["no_videos"] = "Chưa có video nào.", ["play"] = "Phát video",
        ["glossary_lead"] = "Thuật ngữ, từ viết tắt, tên phòng ban và cơ sở tại SGU – kèm bản dịch.",
        ["search_terms"] = "Tìm thuật ngữ, từ viết tắt…", ["translations"] = "Bản dịch",
        ["not_found"] = "Không tìm thấy trang", ["not_found_text"] = "Điểm hoặc trang này không tồn tại hoặc đã bị ẩn.",
        ["about_title"] = "Về CampusGuide",
        ["about_text"] = "Trường Đại học Sài Gòn (SGU) là trường đại học công lập trực thuộc UBND TP. Hồ Chí Minh, trụ sở chính tại 273 An Dương Vương.\n\nCampusGuide là hệ thống hướng dẫn tham quan khuôn viên: mỗi toà nhà, phòng ban có mã QR riêng; quét mã để nghe thuyết minh bằng ngôn ngữ của bạn, xem bản đồ chỉ đường và đi theo các tour gợi ý.",
        ["how_title"] = "Cách sử dụng", ["how_1"] = "Chọn ngôn ngữ ở góc trên.", ["how_2"] = "Mở Bản đồ hoặc chọn một Tour.",
        ["how_3"] = "Đến nơi, quét mã QR dán tại điểm.", ["how_4"] = "Nghe thuyết minh, xem chỉ đường.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["meta_desc"] = "Campus tours, maps and guides for Saigon University",
        ["skip"] = "Skip to content", ["sgu_short"] = "Saigon University", ["sgu_full"] = "Saigon University (SGU)",
        ["nav_home"] = "Home", ["nav_points"] = "Places", ["nav_map"] = "Map", ["nav_tours"] = "Tours",
        ["nav_notices"] = "Notices", ["nav_live"] = "Live", ["nav_glossary"] = "Glossary", 
        ["scan_qr"] = "Scan QR", ["language"] = "Language", 
        ["menu"] = "Menu", ["more"] = "More", ["close"] = "Close", ["about"] = "About", ["explore"] = "Explore", ["info"] = "Information",
        ["footer_about"] = "CampusGuide helps freshmen, visitors and alumni find their way, listen to audio guides and explore the SGU campus in many languages.",
        ["hero_eyebrow"] = "Smart campus guide", ["hero_title"] = "Discover Saigon University",
        ["hero_lead"] = "Campus map, multilingual audio guides, guided tours and QR codes at every building.",
        ["start_tour"] = "Start tour", ["open_map"] = "Open map",
        ["stat_points"] = "places", ["stat_tours"] = "tours", ["stat_langs"] = "languages",
        ["featured_points"] = "Popular places", ["hot_tours"] = "Popular tours", ["latest_notices"] = "Latest notices",
        ["see_all"] = "See all", ["no_points"] = "No places published yet.", ["no_tours"] = "No tours yet.",
        ["no_notices"] = "No notices right now.", ["urgent"] = "Urgent", ["happening"] = "Happening now", ["upcoming"] = "Upcoming",
        ["visitors_now"] = "Visitors right now", ["live_hint"] = "See live visitor numbers and livestreams →",
        ["cta_title"] = "Standing in front of a building?", ["cta_text"] = "Scan the QR code posted at buildings and offices to hear the guide in your language – no app, no sign-in.",
        ["tour_badge"] = "Campus tour", ["stops"] = "stops", ["min"] = "min",
        ["points_lead"] = "Buildings, classrooms, halls, library, canteen… across the campus.",
        ["search_points"] = "Search buildings, rooms, faculties…", ["all"] = "All", ["found_n"] = "{0} places",
        ["view_on_map"] = "View on map", ["no_results"] = "No matching results.", 
        ["back"] = "Back", ["visits"] = "visits", ["reviews"] = "reviews", 
        ["fallback_note"] = "No translation for this language yet – showing the fallback language.",
        ["machine_note"] = "Machine translation – pending review.",
        ["listen"] = "Listen", ["stop"] = "Stop", ["tts_unsupported"] = "Your browser cannot read aloud", ["no_text"] = "No description yet.",
        ["videos"] = "Videos", ["rate_title"] = "Rate this place", ["comment_ph"] = "Your comment (optional)", ["send"] = "Send",
        ["rate_thanks"] = "Thanks for your rating!",
        ["directions"] = "Directions", ["share"] = "Share", ["qr_title"] = "On-site QR code",
        ["qr_text"] = "This code is posted at the place. Scan it to listen to the guide.",
        ["in_tours"] = "Part of tours", ["nearby"] = "Nearby",
        ["qr_invalid"] = "Invalid QR code. Please scan the codes posted at campus buildings.",
        ["map_layer"] = "Switch map layer", ["my_location"] = "My location", ["details"] = "Details", ["you_here"] = "You are here",
        ["geo_denied"] = "Cannot get your location – please allow location access.",
        ["geo_insecure"] = "Browsers only share location on https pages.",
        ["scan_lead"] = "Point your camera at the QR code on a building or office to listen to the guide.",
        ["open_camera"] = "Open camera", ["scan_from_image"] = "Scan from photo", ["enter_code"] = "Or type the code printed under the QR", ["go"] = "Open",
        ["cam_insecure"] = "The camera only works on https pages. Use your phone's camera app, or type the code below.",
        ["cam_denied"] = "Cannot open the camera. Allow camera access or use \"Scan from photo\".",
        ["site_qr_title"] = "QR code to open CampusGuide", ["site_qr_lead"] = "Scan this code with your phone camera to open CampusGuide – no app needed.", ["scan_hint"] = "Open your phone camera and point it at the code", ["scan_points_hint"] = "Every building and office also has its own QR code – scan it to hear that place's guide.",
        ["tours_lead"] = "Campus routes for visitors, freshmen and alumni.",
        ["aud_visitor"] = "Visitors", ["aud_freshman"] = "Freshmen", ["aud_alumni"] = "Alumni",
        ["duration"] = "duration", ["distance"] = "distance", 
        ["itinerary"] = "Itinerary",
        ["notices_lead"] = "Important notices and upcoming events on campus.",
        ["upcoming_events"] = "Upcoming events", ["no_upcoming"] = "No upcoming events.",
        ["live_lead"] = "University introduction videos and event livestreams.",
        ["listening_now"] = "Listening now", ["busy_points"] = "Busiest places", ["live_now"] = "Live now",
        ["intro_videos"] = "Introduction videos", ["no_videos"] = "No videos yet.", ["play"] = "Play video",
        ["glossary_lead"] = "Terms, abbreviations, offices and facilities at SGU – with translations.",
        ["search_terms"] = "Search terms, abbreviations…", ["translations"] = "Translations",
        ["not_found"] = "Page not found", ["not_found_text"] = "This place or page does not exist or is hidden.",
        ["about_title"] = "About CampusGuide",
        ["about_text"] = "Saigon University (SGU) is a public university under the Ho Chi Minh City People's Committee, headquartered at 273 An Duong Vuong.\n\nCampusGuide is the campus visitor guide: every building and office has its own QR code; scan it to hear the guide in your language, get directions and follow suggested tours.",
        ["how_title"] = "How it works", ["how_1"] = "Choose your language at the top.", ["how_2"] = "Open the Map or pick a Tour.",
        ["how_3"] = "On site, scan the posted QR code.", ["how_4"] = "Listen to the guide and get directions.",
    };

    // Ngôn ngữ châu Á: dịch phần menu và các nút chính; phần còn lại dùng tiếng Anh
    private static readonly Dictionary<string, string> Ja = new()
    {
        ["sgu_short"] = "サイゴン大学", ["sgu_full"] = "サイゴン大学 (SGU)", ["nav_home"] = "ホーム", ["nav_points"] = "スポット", ["nav_map"] = "地図",
        ["nav_tours"] = "ツアー", ["nav_notices"] = "お知らせ", ["nav_live"] = "ライブ", ["nav_glossary"] = "用語集", 
        ["scan_qr"] = "QRスキャン", ["language"] = "言語", ["more"] = "その他",
        ["hero_title"] = "サイゴン大学を探検しよう", ["hero_lead"] = "キャンパスマップ、多言語音声ガイド、ツアー、各建物のQRコード。",
        ["start_tour"] = "ツアー開始", ["open_map"] = "地図を開く", ["featured_points"] = "人気スポット", ["hot_tours"] = "人気ツアー",
        ["latest_notices"] = "最新のお知らせ", ["see_all"] = "すべて見る", ["listen"] = "音声ガイドを聞く", ["stop"] = "停止",
        ["directions"] = "道案内", ["all"] = "すべて", ["min"] = "分", ["stops"] = "スポット", ["details"] = "詳細",
        ["stat_points"] = "スポット", ["stat_tours"] = "ツアー", ["stat_langs"] = "言語", ["machine_note"] = "機械翻訳です（確認待ち）。",
    };

    private static readonly Dictionary<string, string> Ko = new()
    {
        ["sgu_short"] = "사이공 대학교", ["sgu_full"] = "사이공 대학교 (SGU)", ["nav_home"] = "홈", ["nav_points"] = "장소", ["nav_map"] = "지도",
        ["nav_tours"] = "투어", ["nav_notices"] = "공지", ["nav_live"] = "라이브", ["nav_glossary"] = "용어집", 
        ["scan_qr"] = "QR 스캔", ["language"] = "언어", ["more"] = "더보기",
        ["hero_title"] = "사이공 대학교를 둘러보세요", ["hero_lead"] = "캠퍼스 지도, 다국어 오디오 가이드, 투어, 건물마다 QR 코드.",
        ["start_tour"] = "투어 시작", ["open_map"] = "지도 열기", ["featured_points"] = "인기 장소", ["hot_tours"] = "인기 투어",
        ["latest_notices"] = "최신 공지", ["see_all"] = "모두 보기", ["listen"] = "오디오 가이드 듣기", ["stop"] = "정지",
        ["directions"] = "길찾기", ["all"] = "전체", ["min"] = "분", ["stops"] = "곳", ["details"] = "자세히",
        ["stat_points"] = "장소", ["stat_tours"] = "투어", ["stat_langs"] = "언어", ["machine_note"] = "기계 번역입니다 (검토 대기 중).",
    };

    private static readonly Dictionary<string, string> Zh = new()
    {
        ["sgu_short"] = "西贡大学", ["sgu_full"] = "西贡大学 (SGU)", ["nav_home"] = "首页", ["nav_points"] = "景点", ["nav_map"] = "地图",
        ["nav_tours"] = "路线", ["nav_notices"] = "通知", ["nav_live"] = "直播", ["nav_glossary"] = "术语", 
        ["scan_qr"] = "扫码", ["language"] = "语言", ["more"] = "更多",
        ["hero_title"] = "探索西贡大学", ["hero_lead"] = "校园地图、多语言语音导览、参观路线，每栋楼都有二维码。",
        ["start_tour"] = "开始参观", ["open_map"] = "打开地图", ["featured_points"] = "热门景点", ["hot_tours"] = "热门路线",
        ["latest_notices"] = "最新通知", ["see_all"] = "查看全部", ["listen"] = "收听讲解", ["stop"] = "停止",
        ["directions"] = "导航", ["all"] = "全部", ["min"] = "分钟", ["stops"] = "站", ["details"] = "详情",
        ["stat_points"] = "景点", ["stat_tours"] = "路线", ["stat_langs"] = "语言", ["machine_note"] = "机器翻译（待审核）。",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> All = new()
    {
        ["vi"] = Vi, ["en"] = En, ["ja"] = Ja, ["ko"] = Ko, ["zh-CN"] = Zh,
    };
}
