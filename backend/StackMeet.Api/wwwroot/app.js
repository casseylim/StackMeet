// Event catalog: Doubles and Timed Relay now expose all three disciplines; Head To Head remains unchanged.
const eventGroups = {
  Individuals: ["3-3-3", "3-6-3", "Cycle"],
  Doubles: ["3-3-3", "3-6-3", "Cycle"],
  "Timed Relay": ["3-3-3", "3-6-3", "Cycle"],
  "Head To Head": ["3-6-3", "Cycle"]
};

const branding = Object.freeze({
  organizationName: "Sistem NADI Track",
  shortName: "NADITrack",
  productName: "NADITrack",
  reportHeader: "Sistem NADI Track",
  browserTitle: "Sistem NADI Track",
  sidebarTitle: "NADI",
  sidebarSubtitle: "Track",
  defaultCompetitionName: "Sistem NADI Track",
  ...(window.StackMeetBranding || {})
});

const STACKMEET_APP_VERSION = "0.9.28";
const stackMeetTimeZone = "Asia/Kuala_Lumpur";
const stackMeetLocale = "en-MY";

function brandText(key) {
  return branding[key] || "";
}

const prelimEntryConfig = {
  "1": { type: "Individual", entryType: "Individual", events: ["3-3-3", "3-6-3", "Cycle"] },
  "2": { type: "Doubles", entryType: "Doubles", events: ["Cycle"] },
  "3": { type: "Timed Relay", entryType: "Relay", events: ["3-6-3"] }
};

const prelimEventFieldIds = {
  "3-3-3": "prelim333",
  "3-6-3": "prelim363",
  Cycle: "prelimCycle"
};

const countries = [
  "Malaysia", "Singapore", "Indonesia", "Thailand", "Philippines", "Vietnam",
  "Brunei", "Cambodia", "Laos", "Myanmar", "China", "Hong Kong", "Taiwan",
  "Japan", "South Korea", "India", "Australia", "New Zealand", "United States",
  "Canada", "United Kingdom", "Germany", "France", "Spain", "Other"
];

const reportPresets = {
  div_counts: { type: "division-counts", group: "", columns: ["division", "count"], team: "" },
  all_individuals: { type: "individuals", group: "", columns: ["id", "name", "division", "country", "region", "doubles_partner", "relay_team", "avg_time", "email"], team: "" },
  paid_status: { type: "individuals", group: "", columns: ["id", "name", "division", "country", "paid", "amt", "email"], team: "" },
  all_doubles: { type: "doubles", group: "", columns: ["id", "name", "country", "region", "division"], team: "" },
  not_doubles: { type: "individuals", group: "", columns: ["id", "name", "country", "region", "doubles_partner", "relay_team", "avg_time", "division", "gender", "email"], team: "not-doubles" },
  all_relay: { type: "timed-relay", group: "", columns: ["id", "name", "country", "region", "division"], team: "" },
  not_relay: { type: "individuals", group: "", columns: ["id", "name", "country", "region", "doubles_partner", "relay_team", "division", "gender", "email"], team: "not-relay" }
};

const competitionReportPresets = {
  individual_prelim_div: { stage: "prelims", type: "i", division: "all-div", event: "all", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  doubles_prelim_div: { stage: "prelims", type: "d", division: "all-div", event: "all-min", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  relay_prelim_div: { stage: "prelims", type: "r", division: "all-div", event: "3-6-3", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  individual_finals_div: { stage: "all", type: "i", division: "all-div", event: "all", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  doubles_finals_div: { stage: "all", type: "d", division: "all-div", event: "all-min", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  relay_finals_div: { stage: "all", type: "r", division: "all-div", event: "3-6-3", limit: "0", gender: "", specialMode: "combined", highlight: "yes" },
  female_soc: { stage: "finals", type: "i", division: "all", event: "all-min", limit: "10", gender: "F", specialMode: "combined", highlight: "yes" },
  male_soc: { stage: "finals", type: "i", division: "all", event: "all-min", limit: "10", gender: "M", specialMode: "combined", highlight: "yes" },
  doubles_soc: { stage: "finals", type: "d", division: "all", event: "cycle", limit: "10", gender: "", specialMode: "combined", highlight: "yes" },
  relay_soc: { stage: "finals", type: "r", division: "all", event: "3-6-3", limit: "10", gender: "", specialMode: "combined", highlight: "yes" },
  top3_female_allaround: { stage: "all", type: "i", division: "all", event: "all-around", limit: "3", gender: "F", specialMode: "combined", highlight: "no" },
  top3_male_allaround: { stage: "all", type: "i", division: "all", event: "all-around", limit: "3", gender: "M", specialMode: "combined", highlight: "no" },
  top3_doubles_cycle: { stage: "all", type: "d", division: "all", event: "cycle", limit: "3", gender: "", specialMode: "combined", highlight: "no" },
  top3_relay_363: { stage: "all", type: "r", division: "all", event: "3-6-3", limit: "3", gender: "", specialMode: "combined", highlight: "no" },
  ss_top3_allaround: { stage: "all", type: "i", division: "all", event: "all-around", limit: "3", gender: "", specialMode: "special", highlight: "no" },
  ss_top3_male_allaround: { stage: "all", type: "i", division: "all", event: "all-around", limit: "3", gender: "M", specialMode: "special", highlight: "no" },
  ss_top3_female_allaround: { stage: "all", type: "i", division: "all", event: "all-around", limit: "3", gender: "F", specialMode: "special", highlight: "no" }
};

const reportColumns = [
  { key: "id", label: "ID", types: ["individuals", "doubles", "timed-relay"] },
  { key: "name", label: "Name", types: ["individuals", "doubles", "timed-relay"] },
  { key: "fname", label: "First Name", types: ["individuals"] },
  { key: "lname", label: "Last Name", types: ["individuals"] },
  { key: "age", label: "Age", types: ["individuals"] },
  { key: "division", label: "Division", types: ["individuals", "doubles", "timed-relay", "division-counts"] },
  { key: "org", label: "Org", types: ["individuals"] },
  { key: "country", label: "Country", types: ["individuals", "doubles", "timed-relay"] },
  { key: "region", label: "Region", types: ["individuals", "doubles", "timed-relay"] },
  { key: "doubles", label: "Reg Doubles", types: ["individuals"] },
  { key: "doubles_partner", label: "Reg Doubles Partner", types: ["individuals"] },
  { key: "st_doubles_partner", label: "ST Doubles Partner", types: ["individuals"] },
  { key: "d_id", label: "ST Doubles ID", types: ["individuals"] },
  { key: "doubles_cp_partner", label: "ST C/P Doubles", types: ["individuals"] },
  { key: "relay", label: "Reg Relay", types: ["individuals"] },
  { key: "relay_team", label: "Reg Relay Name", types: ["individuals"] },
  { key: "r_id", label: "ST Relay ID", types: ["individuals"] },
  { key: "r_name", label: "ST Relay Name", types: ["individuals"] },
  { key: "r_division", label: "ST Relay Division", types: ["individuals"] },
  { key: "paid", label: "Paid", types: ["individuals"] },
  { key: "amt", label: "Registration Fee", types: ["individuals"] },
  { key: "avg_time", label: "Average 3-6-3", types: ["individuals"] },
  { key: "dob", label: "DOB", types: ["individuals"] },
  { key: "phone", label: "Phone", types: ["individuals"] },
  { key: "gender", label: "Gender", types: ["individuals"] },
  { key: "email", label: "Email", types: ["individuals"] },
  { key: "my_ic", label: "MY - IC", types: ["individuals"] },
  { key: "my_class", label: "MY - Class", types: ["individuals"] },
  { key: "special", label: "Special", types: ["individuals"] },
  { key: "checkedIn", label: "Check-In", types: ["individuals"] },
  { key: "count", label: "Stackers", types: ["division-counts"] }
];

const defaultMalayTranslations = {
  "Dashboard": "Papan Pemuka",
  "Settings": "Tetapan",
  "Reports": "Laporan",
  "Competition Reports": "Laporan Pertandingan",
  "Participant": "Peserta",
  "Individual": "Individu",
  "Stackers": "Peserta",
  "Doubles": "Beregu",
  "Relays": "Relay",
  "Print Center": "Pusat Cetakan",
  "All Packets": "Semua Paket",
  "Name Badges": "Lencana Nama",
  "Individual Time Sheets": "Borang Masa Individu",
  "Preliminary Time Sheets": "Borang Masa Awal",
  "Final Time Sheets": "Borang Masa Akhir",
  "Other Print Tools": "Alat Cetakan Lain",
  "Doubles Time Sheets": "Borang Masa Beregu",
  "Relay Time Sheets": "Borang Masa Relay",
  "Final Time Sheets": "Borang Masa Akhir",
  "Individual Finals": "Akhir Individu",
  "Doubles Finals": "Akhir Beregu",
  "Relay Finals": "Akhir Relay",
  "SOC Packet": "Paket SOC",
  "Head To Head Brackets": "Carta Head To Head",
  "Build Brackets": "Bina Carta",
  "Competition": "Pertandingan",
  "Leader Board": "Papan Kedudukan",
  "Awards Planner": "Perancang Anugerah",
  "Awards": "Anugerah",
  "Users": "Pengguna",
  "Language": "Bahasa",
  "Home": "Utama",
  "Setup": "Persediaan",
  "Admin": "Admin",
  "Teams": "Pasukan",
  "Print": "Cetak",
  "Entry": "Kemasukan",
  "Display": "Paparan",
  "Access": "Akses",
  "Export XML": "Eksport XML",
  "Import XML": "Import XML",
  "Local mode": "Mod tempatan",
  "Saved in this browser": "Disimpan dalam pelayar ini",
  "Tournament Snapshot": "Ringkasan Kejohanan",
  "Notifications": "Notifikasi",
  "Mark All Read": "Tanda Semua Dibaca",
  "Competition Settings": "Tetapan Pertandingan",
  "Save Settings": "Simpan Tetapan",
  "Competition Name": "Nama Pertandingan",
  "Type": "Jenis",
  "Start Date": "Tarikh Mula",
  "End Date": "Tarikh Tamat",
  "KBS Logo": "Logo KBS",
  "Prelim Rounds": "Pusingan Awal",
  "Final Rounds": "Pusingan Akhir",
  "Paperless Entry": "Kemasukan Tanpa Kertas",
  "Advance Individuals": "Individu Ke Akhir",
  "Advance Doubles": "Beregu Ke Akhir",
  "Advance C/P Doubles": "Beregu Anak/Ibu Bapa Ke Akhir",
  "Advance Timed Relay": "Relay Masa Ke Akhir",
  "Events": "Acara",
  "Save Events": "Simpan Acara",
  "Divisions": "Divisyen",
  "Save Divisions": "Simpan Divisyen",
  "Generated Divisions": "Divisyen Dijana",
  "Add Custom Division": "Tambah Divisyen Khas",
  "Individuals": "Individu",
  "Save Stacker": "Simpan Peserta",
  "Print Time Sheet": "Cetak Borang Masa",
  "Print Range": "Cetak Julat",
  "Print Finals": "Cetak Akhir",
  "Finals:": "Akhir:",
  "Stacker": "Peserta",
  "Prelims": "Awal",
  "Attempt 1": "Percubaan 1",
  "Attempt 2": "Percubaan 2",
  "Attempt 3": "Percubaan 3",
  "Best Time": "Masa Terbaik",
  "Place": "Tempat",
  "All Final Time Sheets": "Semua Borang Masa Akhir",
  "Individual Final Time Sheets": "Borang Masa Akhir Individu",
  "Doubles Final Time Sheets": "Borang Masa Akhir Beregu",
  "Relay Final Time Sheets": "Borang Masa Akhir Relay",
  "sheets ready for judges": "borang sedia untuk hakim",
  "sheet ready for judges": "borang sedia untuk hakim",
  "No final sheets yet. Enter prelim results first.": "Belum ada borang akhir. Masukkan keputusan awal dahulu.",
  "No finalists matched this selection.": "Tiada peserta akhir untuk pilihan ini.",
  "Start at the top of the page, allow 2 warm-ups prior to Attempt 1 for each stacker.": "Mula dari bahagian atas halaman, benarkan 2 pemanasan sebelum Percubaan 1 untuk setiap peserta.",
  "After warm-ups, the next 3 stacks must be used as Attempt 1, 2 and 3.": "Selepas pemanasan, 3 susunan seterusnya mesti digunakan sebagai Percubaan 1, 2 dan 3.",
  "Indicate time using all numbers as displayed on the timer. Example: 6.523.": "Tulis masa menggunakan semua nombor seperti dipaparkan pada pemasa. Contoh: 6.523.",
  "SCRATCH write 999.": "SCRATCH tulis 999.",
  "Leave blank = did not compete.": "Biarkan kosong = tidak bertanding.",
  "Cancel Edit": "Batal Sunting",
  "Name": "Nama",
  "Gender": "Jantina",
  "Date Of Birth": "Tarikh Lahir",
  "Age": "Umur",
  "Generated Division": "Divisyen Dijana",
  "Custom Division": "Divisyen Khas",
  "Organization": "Organisasi",
  "Country": "Negara",
  "Region": "Negeri / Kawasan",
  "Paid": "Dibayar",
  "Checked In": "Daftar Masuk",
  "Search stackers": "Cari peserta",
  "Import Stackers CSV": "Import CSV Peserta",
  "Add Team": "Tambah Pasukan",
  "Generated Type": "Jenis Dijana",
  "Status": "Status",
  "Search Stacker / Child": "Cari Peserta / Anak",
  "Stacker / Child": "Peserta / Anak",
  "Search Registered Partner": "Cari Rakan Berdaftar",
  "Registered Partner": "Rakan Berdaftar",
  "Parent / Guardian": "Ibu Bapa / Penjaga",
  "Completed": "Lengkap",
  "Incomplete": "Belum Lengkap",
  "All": "Semua",
  "Location": "Lokasi",
  "Edit": "Sunting",
  "Need Partner": "Perlu Rakan",
  "Complete": "Lengkap",
  "Normal Doubles": "Beregu Biasa",
  "Child / Parent": "Anak / Ibu Bapa",
  "Save Doubles": "Simpan Beregu",
  "Edit Doubles": "Sunting Beregu",
  "Reports Center": "Pusat Laporan",
  "Build Results": "Bina Keputusan",
  "Run Report": "Jana Laporan",
  "Print Report": "Cetak Laporan",
  "Export CSV": "Eksport CSV",
  "Export Excel": "Eksport Excel",
  "Bahasa Malaysia Translation Setup": "Tetapan Terjemahan Bahasa Malaysia",
  "Language Translation Setup": "Tetapan Terjemahan Bahasa",
  "Save Language": "Simpan Bahasa",
  "Active Language": "Bahasa Aktif",
  "Search Translation": "Cari Terjemahan",
  "English": "Inggeris",
  "Bahasa Malaysia": "Bahasa Malaysia"
};

const defaultChineseTranslations = {
  "Dashboard": "仪表板",
  "Settings": "设置",
  "Reports": "报告",
  "Stackers": "选手",
  "Doubles": "双人",
  "Relay": "接力",
  "Print Center": "打印中心",
  "All Packets": "全部资料包",
  "Name Badges": "姓名牌",
  "Individual Time Sheets": "个人计时表",
  "Doubles Time Sheets": "双人计时表",
  "Relay Time Sheets": "接力计时表",
  "Final Time Sheets": "决赛计时表",
  "Individual Finals": "个人决赛",
  "Doubles Finals": "双人决赛",
  "Relay Finals": "接力决赛",
  "SOC Packet": "SOC 资料包",
  "Head To Head Brackets": "对战赛程表",
  "Build Brackets": "生成赛程表",
  "Competition": "比赛",
  "Leader Board": "排行榜",
  "Awards Planner": "奖杯奖牌规划",
  "Awards": "奖项",
  "Users": "用户",
  "Language": "语言",
  "Home": "主页",
  "Setup": "设置",
  "Admin": "管理",
  "Teams": "队伍",
  "Print": "打印",
  "Entry": "录入",
  "Display": "显示",
  "Access": "权限",
  "Export XML": "导出 XML",
  "Import XML": "导入 XML",
  "Local mode": "本地模式",
  "Saved in this browser": "已保存在此浏览器",
  "Tournament Snapshot": "赛事概览",
  "Notifications": "通知",
  "Mark All Read": "全部标为已读",
  "Competition Settings": "赛事设置",
  "Save Settings": "保存设置",
  "Competition Name": "比赛名称",
  "Type": "类型",
  "Start Date": "开始日期",
  "End Date": "结束日期",
  "KBS Logo": "KBS 标志",
  "Prelim Rounds": "预赛轮次",
  "Final Rounds": "决赛轮次",
  "Paperless Entry": "无纸化录入",
  "Advance Individuals": "个人晋级人数",
  "Advance Doubles": "双人晋级队数",
  "Advance C/P Doubles": "亲子双人晋级队数",
  "Advance Timed Relay": "接力晋级队数",
  "Events": "项目",
  "Save Events": "保存项目",
  "Divisions": "组别",
  "Save Divisions": "保存组别",
  "Generated Divisions": "自动组别",
  "Add Custom Division": "添加自定义组别",
  "Individuals": "个人",
  "Save Stacker": "保存选手",
  "Print Time Sheet": "打印计时表",
  "Print Range": "打印范围",
  "Print Finals": "打印决赛",
  "Finals:": "决赛：",
  "Stacker": "选手",
  "Prelims": "预赛",
  "Attempt 1": "第 1 次",
  "Attempt 2": "第 2 次",
  "Attempt 3": "第 3 次",
  "Best Time": "最佳时间",
  "Place": "名次",
  "All Final Time Sheets": "全部决赛计时表",
  "Individual Final Time Sheets": "个人决赛计时表",
  "Doubles Final Time Sheets": "双人决赛计时表",
  "Relay Final Time Sheets": "接力决赛计时表",
  "sheets ready for judges": "张表可交给裁判",
  "sheet ready for judges": "张表可交给裁判",
  "No final sheets yet. Enter prelim results first.": "还没有决赛计时表。请先录入预赛成绩。",
  "No finalists matched this selection.": "此选择没有符合的决赛选手。",
  "Start at the top of the page, allow 2 warm-ups prior to Attempt 1 for each stacker.": "从页面最上方开始，每位选手在第 1 次正式尝试前可热身 2 次。",
  "After warm-ups, the next 3 stacks must be used as Attempt 1, 2 and 3.": "热身后，接下来的 3 次叠杯必须记录为第 1、2、3 次尝试。",
  "Indicate time using all numbers as displayed on the timer. Example: 6.523.": "按计时器显示完整记录时间。例如：6.523。",
  "SCRATCH write 999.": "犯规写 999。",
  "Leave blank = did not compete.": "留空 = 没有参赛。",
  "Cancel Edit": "取消编辑",
  "Name": "姓名",
  "Gender": "性别",
  "Date Of Birth": "出生日期",
  "Age": "年龄",
  "Generated Division": "自动组别",
  "Custom Division": "自定义组别",
  "Organization": "组织",
  "Country": "国家",
  "Region": "州 / 地区",
  "Paid": "已付款",
  "Checked In": "已报到",
  "Search stackers": "搜索选手",
  "Import Stackers CSV": "导入选手 CSV",
  "Add Team": "添加队伍",
  "Generated Type": "自动类型",
  "Status": "状态",
  "Search Stacker / Child": "搜索选手 / 孩子",
  "Stacker / Child": "选手 / 孩子",
  "Search Registered Partner": "搜索已注册搭档",
  "Registered Partner": "已注册搭档",
  "Parent / Guardian": "父母 / 监护人",
  "Completed": "完整",
  "Incomplete": "未完整",
  "All": "全部",
  "Location": "地点",
  "Edit": "编辑",
  "Need Partner": "需要搭档",
  "Complete": "完整",
  "Normal Doubles": "普通双人",
  "Child / Parent": "亲子双人",
  "Save Doubles": "保存双人",
  "Edit Doubles": "编辑双人",
  "Reports Center": "报告中心",
  "Build Results": "生成成绩",
  "Run Report": "运行报告",
  "Print Report": "打印报告",
  "Export CSV": "导出 CSV",
  "Export Excel": "导出 Excel",
  "Bahasa Malaysia Translation Setup": "语言翻译设置",
  "Language Translation Setup": "语言翻译设置",
  "Save Language": "保存语言",
  "Active Language": "当前语言",
  "Search Translation": "搜索翻译",
  "English": "英文",
  "Bahasa Malaysia": "马来文",
  "Simplified Chinese": "简体中文"
};

defaultChineseTranslations["Competition Reports"] = "赛事报告";
defaultChineseTranslations.Participant = "参赛者";
defaultChineseTranslations.Individual = "个人";
defaultChineseTranslations.Relays = "接力";
defaultChineseTranslations["Preliminary Time Sheets"] = "预赛计时表";
defaultChineseTranslations["Final Time Sheets"] = "决赛计时表";
defaultChineseTranslations["Other Print Tools"] = "其他打印工具";

const defaultTranslationPacks = {
  ms: defaultMalayTranslations,
  zh: defaultChineseTranslations
};

const divisionAges = Array.from({ length: 102 }, (_, index) => index + 4);
const monthNames = [
  "january", "february", "march", "april", "may", "june",
  "july", "august", "september", "october", "november", "december"
];

const defaultDivisionSettings = {
  combined: [6, 14, 18, 24, 64],
  male: [8, 10, 12, 100],
  female: [9, 12],
  special: [10, 14, 18],
  doubles: [10, 12, 14, 18],
  childParentDoubles: [10],
  specialDoubles: [12, 18],
  specialChildParentDoubles: [10],
  timedRelay: [10, 12, 14, 18],
  headToHeadRelay: [10, 12, 14, 18],
  custom: []
};

const defaultAwards = {
  individualPlaces: 5,
  individualItems: ["Medal", "Medal", "Medal", "Medal", "Medal"],
  doublesPlaces: 3,
  doublesItems: ["Trophy", "Medal", "Medal", "Medal", "Medal"],
  relayPlaces: 3,
  relayUnits: 4,
  relayItems: ["Trophy", "Medal", "Medal", "Medal", "Medal"],
  overall: {
    male: { limit: 3, item: "Trophy" },
    female: { limit: 3, item: "Trophy" },
    specialMale: { limit: 3, item: "Trophy" },
    specialFemale: { limit: 3, item: "Trophy" },
    combined: { limit: 3, item: "Trophy" }
  }
};

const awardOverallGroups = [
  { key: "male", label: "Top Male", basis: "Normal male stackers" },
  { key: "female", label: "Top Female", basis: "Normal female stackers" },
  { key: "specialMale", label: "Top Special Male", basis: "Special male stackers" },
  { key: "specialFemale", label: "Top Special Female", basis: "Special female stackers" },
  { key: "combined", label: "Top Overall Combined", basis: "All normal stackers combined" }
];

const demo = {
  settings: {
    name: brandText("defaultCompetitionName"),
    type: "Sanctioned",
    start: todayIsoDate(),
    end: todayIsoDate(),
    prelims: "1",
    finals: "1",
    kbsLogo: "No",
    soc: "Yes",
    prelimTimes: "best",
    paperless: "Yes",
    advanceIndividuals: 10,
    advanceDoubles: 6,
    advanceCpDoubles: 5,
    advanceRelay: 0,
    timeSheetInput: "blank",
    language: "en",
    ageCalculationMode: "actual",
    separateSpecialDivisionsByGender: false
  },
  translations: {
    ms: structuredClone(defaultMalayTranslations),
    zh: structuredClone(defaultChineseTranslations)
  },
  leaderboard: {
    type: "Divisional Results",
    stage: "Prelims",
    bg: "Black",
    color: "Blue",
    pause: 8,
    fontSize: 1,
    progressHeight: 4,
    limit: 10
  },
  awards: structuredClone(defaultAwards),
  events: {
    Individuals: ["3-3-3", "3-6-3", "Cycle"],
    Doubles: ["3-3-3", "3-6-3", "Cycle"],
    "Timed Relay": ["3-3-3", "3-6-3", "Cycle"],
    "Head To Head": ["3-6-3"]
  },
  divisionSettings: structuredClone(defaultDivisionSettings),
  divisions: ["6U C", "7-8 M", "7-9 F", "9-10 M", "10-12 F", "11-12 M", "13-14 C", "15-18 C", "SS 7-10 L1", "SS 11-14 L1", "SS 15-18 L1", "Child/Parent 10U", "Child/Parent 11+"],
  stackers: [
    { id: "1.1", name: "Avery Tan", gender: "M", dob: "2015-05-14", org: "SMK Mambau", division: "11-12 M", country: "Malaysia", paid: "Yes", special: "No" },
    { id: "1.2", name: "Maya Lim", gender: "F", dob: "2014-08-20", org: "SJKC San Min", division: "10-12 F", country: "Malaysia", paid: "Yes", special: "No" },
    { id: "1.3", name: "Noah Wong", gender: "M", dob: "2016-03-02", org: "Speed Stars", division: "9-10 M", country: "Malaysia", paid: "No", special: "No" },
    { id: "1.4", name: "Sofia Chen", gender: "F", dob: "2012-11-18", org: "Rapid Stack", division: "13-14 C", country: "Malaysia", paid: "Yes", special: "No" },
    { id: "1.5", name: "Ryan Goh", gender: "M", dob: "2013-01-09", org: "Cup Velocity", division: "SS 11-14 L1", country: "Malaysia", paid: "Yes", special: "Yes" },
    { id: "1.6", name: "Nur Alya", gender: "F", dob: "2010-04-26", org: "Cup Velocity", division: "SS 15-18 L1", country: "Malaysia", paid: "Yes", special: "Yes" }
  ],
  doubles: [
    { id: "2.1", one: "1.1", two: "1.2", division: "Child/Parent 11+", country: "Malaysia" },
    { id: "2.2", one: "1.3", two: "1.4", division: "Child/Parent 10U", country: "Malaysia" }
  ],
  relays: [],
  results: [
    { id: "r1", stage: "Prelims", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [2.911, 2.842, 2.901], penalty: 0 },
    { id: "r2", stage: "Prelims", type: "Individual", participant: "1.2", event: "3-3-3", attempts: [3.101, 3.004, 3.012], penalty: 0 },
    { id: "r3", stage: "Prelims", type: "Individual", participant: "1.4", event: "Cycle", attempts: [8.561, 8.402, 8.511], penalty: 0 },
    { id: "r4", stage: "Finals", type: "Doubles", participant: "2.1", event: "Cycle", attempts: [7.901, 7.842, 7.990], penalty: 0 }
  ],
  notifications: [
    { id: "n1", title: "1 new online registration imported", time: "Jun 29, 7:25 pm", read: false },
    { id: "n2", title: "3 new online registrations imported", time: "Jun 29, 9:20 am", read: false },
    { id: "n3", title: "Tournament data synced", time: "Jul 5, 8:30 pm", read: true }
  ],
  users: [
    { name: "Cassey", access: "Tournament Director", last: "2026-07-10 13:50", platform: "Windows", browser: "Chrome" },
    { name: "Tablet", access: "Data Entry", last: "2026-07-05 07:19", platform: "Android", browser: "Chrome" },
    { name: "Staff Desk", access: "Staff", last: "2026-07-04 22:40", platform: "Windows", browser: "Edge" }
  ]
};

const CompetitionRepository = window.StackMeetStorage.Repository;
const repository = new CompetitionRepository();
const SqlStackerApi = window.StackMeetStorage.StackerApi;
const stackerApi = new SqlStackerApi();
const CompetitionStateProvider = window.StackMeetStorage.ApiProvider;
const BestResultEngine = window.StackMeetBestResult || (() => {
  const statusOrder = { valid: 0, scratch: 1, invalid: 2, missing: 3 };
  const numericAttempts = attempts => (Array.isArray(attempts) ? attempts : [])
    .map(value => value === "" || value === null || value === undefined ? NaN : Number(value))
    .filter(Number.isFinite);
  const validAttempts = attempts => numericAttempts(attempts).filter(value => value > 0 && value < 999);
  const isScratchAttempt = value => Number(value) === 999;
  const calculateBestResult = input => {
    const result = Array.isArray(input) ? { attempts: input } : (input || {});
    const values = numericAttempts(result.attempts);
    const valid = validAttempts(values);
    const bestTime = valid.length ? Math.min(...valid) : null;
    if (bestTime !== null) return { status: "valid", bestTime, bestValidTime: bestTime, eligibleForRanking: true };
    if (!values.length) return { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    if (values.every(isScratchAttempt) || Number(result.penalty) >= 999) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    return { status: "invalid", bestTime: null, bestValidTime: null, eligibleForRanking: false };
  };
  const bestTime = input => calculateBestResult(input).bestTime;
  const appliedPenalty = input => {
    const result = Array.isArray(input) ? {} : (input || {});
    const penalty = Number(result.penalty || 0);
    return penalty > 0 && penalty < 999 ? penalty : 0;
  };
  const rankingTime = input => {
    const summary = calculateBestResult(input);
    return summary.eligibleForRanking ? summary.bestTime + appliedPenalty(input) : Infinity;
  };
  return { statusOrder, numericAttempts, finiteAttempts: numericAttempts, validAttempts, isScratchAttempt, calculateBestResult, classifyResult: calculateBestResult, bestTime, appliedPenalty, rankingTime };
})();
const FinalsReportEngine = window.StackMeetFinalsReports;
const sqlCompetitionSessionKey = "stackmeet-sql-competition-id";
let ageCalculationMode = "actual";
let state = createInitialState();
let pendingSave = Promise.resolve();
let queuedSaveCount = 0;
let route = location.hash.replace("#", "") || "dashboard";
let flashMessage = null;
let editingStackerId = "";
let stackerFormVisible = false;
let focusStackerListAfterRender = false;
let pendingDeleteStackerId = "";
let selectedSqlCompetitionId = null;
let sqlCompetition = null;
let stackerRefreshInFlight = false;
let dashboardPollTimer = null;
let stackerSort = { key: "id", direction: "asc" };
let reportTab = "finals";
let adminReportSort = { index: -1, direction: "asc" };
let adminPrintOrientation = "landscape";
let activePrelimParticipantId = "";
let activePrelimParticipantType = "";
let prelimSaveInFlight = null;
let activeFinalSheetId = "";
let doublesTab = "completed";
let doubleFlashMessage = null;
let editingDoubleId = "";
let stackerDoubleEditorOpen = false;
let relayTab = "ready";
let relayFlashMessage = null;
let editingRelayId = "";
let leaderboardTimer = null;
let leaderboardSlideIndex = 0;

//const routes = [
  //["dashboard", "Dashboard"],
  //["settings", "Settings"],
  //["language", "Language"],
  //["reports", "Reports"],
  //["stackers", "Participant"],
  //["awards", "Awards Planner"],
  //["competition", "Competition"],
  //["leaderboard", "Leader Board"]
//];

const routes = [
    ["dashboard", "Dashboard"],
    ["settings", "Settings"],
    ["language", "Language"],
    ["reports", "Reports"],
    ["stackers", "Participant"],
    ["awards", "Awards Planner"],
    ["competition", "Competition"]
];

const view = document.getElementById("view");
const pageTitle = document.getElementById("pageTitle");
const hero = document.getElementById("dashboardHero");

function createInitialState() {
  return normalizeState(structuredClone(demo));
}

async function loadState() {
  const stored = await repository.load();
  if (stored) return normalizeState(withoutLegacyStackers(stored));

  const initialState = createInitialState();
  await repository.save(legacyStateForSave(initialState));
  return normalizeState(withoutLegacyStackers(initialState));
}

function withoutLegacyStackers(data) {
  const legacy = structuredClone(data);
  legacy.stackers = [];
  return legacy;
}

function legacyStateForSave(data) {
  const legacy = structuredClone(data);
  legacy.stackers = [];
  delete legacy.settings?.ageCalculationMode;
  return legacy;
}

function normalizeState(data) {
  data.settings = {
    ...structuredClone(demo.settings),
    ...(data.settings || {})
  };
  data.translations = Object.fromEntries(Object.entries(defaultTranslationPacks).map(([code, pack]) => [
    code,
    { ...structuredClone(pack), ...(data.translations?.[code] || {}) }
  ]));
  data.settings.prelims = normalizePrelimRounds(data.settings.prelims);
  data.settings.ageCalculationMode = data.settings.ageCalculationMode === "yearBorn" ? "yearBorn" : "actual";
  ageCalculationMode = data.settings.ageCalculationMode;
  data.divisionSettings = {
    ...structuredClone(defaultDivisionSettings),
    ...(data.divisionSettings || {})
  };
  data.divisionSettings.custom = data.divisionSettings.custom || [];
  data.divisionSettings.special = data.divisionSettings.special || [];
  data.divisionSettings.doubles = data.divisionSettings.doubles || structuredClone(defaultDivisionSettings.doubles);
  data.divisionSettings.childParentDoubles = data.divisionSettings.childParentDoubles || structuredClone(defaultDivisionSettings.childParentDoubles);
  data.divisionSettings.specialDoubles = data.divisionSettings.specialDoubles || structuredClone(defaultDivisionSettings.specialDoubles);
  data.divisionSettings.specialChildParentDoubles = data.divisionSettings.specialChildParentDoubles || structuredClone(defaultDivisionSettings.specialChildParentDoubles);
  data.divisionSettings.timedRelay = data.divisionSettings.timedRelay || structuredClone(defaultDivisionSettings.timedRelay);
  data.divisionSettings.headToHeadRelay = data.divisionSettings.headToHeadRelay || structuredClone(defaultDivisionSettings.headToHeadRelay);
  data.leaderboard = normalizeLeaderboard(data.leaderboard);
  data.awards = normalizeAwards(data.awards);
  data.divisions = generateDivisionNames(data.divisionSettings);
  data.stackers = (data.stackers || []).map(stacker => ({
    dob: "",
    age: "",
    customDivision: "",
    standardDivision: "",
    special: "No",
    checkedIn: "No",
    ...stacker
  }));
  data.stackers = recalculateStackerDivisions(data.stackers, data.divisionSettings, data.settings?.start, data.settings.separateSpecialDivisionsByGender === true);
  data.doubles = normalizeDoubles(data.doubles || []);
  data.relays = normalizeRelays(data.relays || [], data.stackers);
  data.results = normalizeResults(data.results || []);
  data.auditLogs = normalizeCompetitionAuditLogs(data.auditLogs || []);
  data.finalQualificationSnapshots = (data.finalQualificationSnapshots || []).map(snapshot => ({
    id: snapshot.id || crypto.randomUUID(), competitionKey: snapshot.competitionKey || currentCompetitionKey(),
    participantType: snapshot.participantType || "Individual", division: snapshot.division || "", event: snapshot.event || "",
    ruleVersion: snapshot.ruleVersion || "final-qualification-v1", sourcePreliminaryResults: snapshot.sourcePreliminaryResults || [],
    selectedQualifiers: snapshot.selectedQualifiers || [], tieException: snapshot.tieException || { required: false, decision: "", rationale: "" },
    generatedAtUtc: snapshot.generatedAtUtc || "", generatedBy: snapshot.generatedBy || "", approvedAtUtc: snapshot.approvedAtUtc || "", approvedBy: snapshot.approvedBy || "",
    status: ["Draft", "Approved", "Superseded"].includes(snapshot.status) ? snapshot.status : "Draft", reconstructed: snapshot.reconstructed === true
  }));
  data.divisions = appendStandardImportedDivisions(data.divisions, data.stackers);
  return data;
}

function normalizeLeaderboard(leaderboard = {}) {
  const merged = {
    ...structuredClone(demo.leaderboard),
    ...leaderboard
  };
  merged.pause = clampNumber(numericFromSetting(merged.pause), 8, 1, 300);
  merged.fontSize = clampNumber(numericFromSetting(merged.fontSize), 1, 0.5, 2);
  merged.progressHeight = clampNumber(numericFromSetting(merged.progressHeight), 4, 1, 20);
  merged.limit = clampNumber(numericFromSetting(merged.limit), 10, 3, 50);
  return merged;
}

function numericFromSetting(value) {
  if (typeof value === "number") return value;
  const match = String(value || "").match(/[\d.]+/);
  return match ? Number(match[0]) : NaN;
}

function clampNumber(value, fallback, min, max) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return fallback;
  return Math.min(max, Math.max(min, numeric));
}

function currentCompetitionKey() {
  return sessionStorage.getItem(sqlCompetitionSessionKey) || window.StackMeetAuth?.competitionId?.() || state?.settings?.competitionKey || state?.settings?.name || "local";
}

function normalizeAwards(awards = {}) {
  const merged = {
    ...structuredClone(defaultAwards),
    ...awards,
    overall: {
      ...structuredClone(defaultAwards.overall),
      ...(awards.overall || {})
    }
  };
  merged.individualPlaces = normalizeAwardLimit(merged.individualPlaces, 5);
  merged.doublesPlaces = normalizeAwardLimit(merged.doublesPlaces, 3);
  merged.relayPlaces = normalizeAwardLimit(merged.relayPlaces, 3);
  merged.relayUnits = normalizeRelayAwardUnits(merged.relayUnits);
  merged.individualItems = normalizeAwardItems(merged.individualItems, merged.individualPlaces);
  merged.doublesItems = normalizeAwardItems(merged.doublesItems, merged.doublesPlaces);
  merged.relayItems = normalizeAwardItems(merged.relayItems, merged.relayPlaces);
  awardOverallGroups.forEach(group => {
    merged.overall[group.key] = {
      limit: normalizeAwardLimit(merged.overall[group.key]?.limit, 3),
      item: normalizeAwardItem(merged.overall[group.key]?.item)
    };
  });
  return merged;
}

function normalizeAwardLimit(value, fallback) {
  const limit = Number(value);
  return [0, 3, 5, 10].includes(limit) ? limit : fallback;
}

function normalizeRelayAwardUnits(value) {
  const units = Number(value);
  return [4, 5, 6].includes(units) ? units : 4;
}

function normalizeAwardItems(items, places) {
  const list = Array.isArray(items) ? items : [];
  return Array.from({ length: Math.max(places, 5) }, (_, index) => normalizeAwardItem(list[index] || "Medal"));
}

function normalizeAwardItem(value) {
  return value === "Trophy" ? "Trophy" : "Medal";
}

function normalizeDoubles(doubles) {
  return doubles.map(team => {
    const type = team.type || (String(team.division || "").toLowerCase().includes("parent") ? "child_parent" : "normal");
    const one = team.one || team.stackerOneId || team.childStackerId || "";
    const two = team.two || team.stackerTwoId || team.parentStackerId || "";
    const status = team.status || (two || team.parentName || type === "child_parent" ? "complete" : "pending");
    return {
      type,
      status,
      one,
      two,
      parentName: team.parentName || team.partnerName || "",
      customDivision: team.customDivision || "",
      division: team.customDivision || team.division || "",
      country: team.country || "Malaysia",
      ...team,
      type,
      status,
      one,
      two,
      parentName: team.parentName || team.partnerName || "",
      customDivision: team.customDivision || ""
    };
  });
}

function normalizeRelays(relays, stackers = []) {
  return relays.map(team => {
    const members = relayMemberIds(team).slice(0, 6);
    const country = team.country || members.map(id => stackers.find(stacker => stacker.id === id)?.country).find(Boolean) || "Malaysia";
    return {
      id: team.id || "",
      name: team.name || "",
      coordinator: team.coordinator || "",
      email: team.email || "",
      phone: team.phone || team.cell || "",
      timedRelayDivision: team.timedRelayDivision || team.timedDivision || team.customDivision || team.division || "",
      headToHeadDivision: team.headToHeadDivision || team.hthDivision || "",
      // Keep legacy fields while older XML and saved competitions are still supported.
      customDivision: team.customDivision || "",
      division: team.timedRelayDivision || team.timedDivision || team.customDivision || team.division || "",
      org: team.org || "",
      country,
      region: team.region || team.loc || "",
      members
    };
  });
}

function normalizePrelimRounds(value) {
  return Number(value) >= 1 ? "1" : "0";
}

function normalizeResults(results) {
  return results.map(result => {
    const participant = String(result.participant || "");
    if (participant.startsWith("2.") && result.type !== "Doubles") return { ...result, type: "Doubles" };
    if (participant.startsWith("3.") && !["Timed Relay", "Relay"].includes(result.type)) return { ...result, type: "Timed Relay" };
    if (result.type === "Relay") return { ...result, type: "Timed Relay" };
    return result;
  });
}

// Normalizes per-competition audit entries stored inside the competition JSON state.
function normalizeCompetitionAuditLogs(logs) {
  return (Array.isArray(logs) ? logs : []).map(log => ({
    id: log.id || crypto.randomUUID(),
    atUtc: log.atUtc || log.at || new Date().toISOString(),
    actorUserId: log.actorUserId ?? null,
    actorEmail: log.actorEmail || "",
    actorName: log.actorName || "",
    action: log.action || "competition.unknown",
    entityType: log.entityType || "",
    entityId: log.entityId || "",
    summary: log.summary || "",
    before: log.before ?? null,
    after: log.after ?? null
  }));
}

// Appends one audit entry to the current competition JSON state after a successful action.
function appendCompetitionAuditLog({ action, entityType, entityId = "", summary = "", before = null, after = null }) {
  const actor = currentAuditActor();
  state.auditLogs = normalizeCompetitionAuditLogs(state.auditLogs || []);
  state.auditLogs.push({
    id: crypto.randomUUID(),
    atUtc: new Date().toISOString(),
    actorUserId: actor.userId,
    actorEmail: actor.email,
    actorName: actor.name,
    action,
    entityType,
    entityId: String(entityId || ""),
    summary,
    before: auditSnapshot(before),
    after: auditSnapshot(after)
  });
}

// Reads the current browser session identity for competition audit attribution.
function currentAuditActor() {
  const session = window.StackMeetAuth?.readSession?.() || {};
  return {
    userId: session.userId ?? null,
    email: session.email || "",
    name: session.displayName || ""
  };
}

// Clones audit values through JSON to avoid storing live object references.
function auditSnapshot(value) {
  if (value === null || value === undefined) return null;
  return JSON.parse(JSON.stringify(value));
}

// Persists an audit-only update without rolling back the completed user action.
async function persistCompetitionAuditLog() {
  try {
    await saveState();
  } catch (error) {
    console.error("Unable to save competition audit log.", error);
  }
}

function saveState() {
  const stateToSave = legacyStateForSave(state);
  queuedSaveCount += 1;
  setSaveStatus("Saving...", "saving");

  const queuedSave = pendingSave
    .catch(() => undefined)
    .then(() => repository.save(stateToSave));

  pendingSave = queuedSave;
  return queuedSave.then(
    () => {
      queuedSaveCount -= 1;
      if (!queuedSaveCount) setSaveStatus("Saved", "saved");
    },
    error => {
      queuedSaveCount -= 1;
      setSaveStatus("Save Failed", "failed");
      throw error;
    }
  );
}

function setSaveStatus(message, stateName) {
  const indicator = document.getElementById("saveStatus");
  if (!indicator) return;
  indicator.textContent = message;
  indicator.dataset.state = stateName;
}

function sqlCompetitionIdFromUrl() {
  const value = new URLSearchParams(location.search).get("competitionId");
  const id = Number(value);
  return Number.isInteger(id) && id > 0 ? id : null;
}

function setSelectedSqlCompetition(competition) {
  selectedSqlCompetitionId = Number(competition.id);
  sqlCompetition = competition;
  sessionStorage.setItem(sqlCompetitionSessionKey, String(selectedSqlCompetitionId));
  const url = new URL(location.href);
  url.searchParams.set("competitionId", String(selectedSqlCompetitionId));
  history.replaceState(null, "", url);
}

function defaultSqlCompetition(competitions) {
  const active = competitions.filter(item => String(item.status || "").toLowerCase() === "active");
  const candidates = active.length ? active : competitions;
  return [...candidates].sort((a, b) => Number(b.id) - Number(a.id))[0] || null;
}

async function initializeSqlNativeStackers() {
  const competitions = await stackerApi.listCompetitions();
  const requested = sqlCompetitionIdFromUrl() || Number(sessionStorage.getItem(sqlCompetitionSessionKey));
  const selected = competitions.find(item => item.id === requested) || defaultSqlCompetition(competitions);
  if (!selected) return;
  setSelectedSqlCompetition(selected);
  await loadCompetitionAgeCalculation();
  await refreshSqlStackers({ allowEditing: true, rerender: false });
}

function usesAuthenticatedCompetitionState() {
  const session = window.StackMeetAuth?.readSession?.();
  return Boolean(session?.token && !session.localFileTest);
}

function competitionSettingsProvider() {
  if (!selectedSqlCompetitionId) throw new Error("A selected competition is required.");
  return new CompetitionStateProvider(`competition-${selectedSqlCompetitionId}-settings`);
}

function applyCompetitionAgeCalculation(mode) {
  ageCalculationMode = mode === "yearBorn" ? "yearBorn" : "actual";
  state.settings.ageCalculationMode = ageCalculationMode;
  state.stackers = recalculateStackerDivisions(state.stackers, state.divisionSettings, state.settings.start, state.settings.separateSpecialDivisionsByGender === true);
  state.divisions = appendStandardImportedDivisions(
    [...generateDivisionNames(state.divisionSettings), ...state.stackers.map(stacker => stacker.division).filter(Boolean)],
    state.stackers
  );
}

async function loadCompetitionAgeCalculation() {
  if (usesAuthenticatedCompetitionState()) {
    applyCompetitionAgeCalculation(state.settings.ageCalculationMode);
    return;
  }

  const saved = await competitionSettingsProvider().load();
  applyCompetitionAgeCalculation(saved?.ageCalculationMode);
}

async function saveCompetitionAgeCalculation(mode) {
  if (usesAuthenticatedCompetitionState()) return;
  await competitionSettingsProvider().save({ ageCalculationMode: mode });
}

function sqlStackerToRuntime(record) {
  const name = [record.firstName, record.lastName === "-" ? "" : record.lastName].filter(Boolean).join(" ").trim();
  const stacker = {
    sqlId: record.id,
    id: record.stackerCode,
    name,
    gender: String(record.gender || "M").toUpperCase().startsWith("F") ? "F" : "M",
    dob: record.birthDate || "",
    age: ageOnCompetitionDate(record.birthDate, state.settings.start) || "",
    customDivision: record.customDivision || "",
    special: record.isSpecialStacker ? "Yes" : "No",
    org: record.club || "Independent",
    country: record.country || "Malaysia",
    region: record.region || "",
    email: record.email || "",
    phone: record.phone || "",
    paid: record.paid || "No",
    checkedIn: record.checkedIn || "No"
  };
  stacker.division = divisionForStacker(stacker, state.divisionSettings, state.settings.start, state.settings.separateSpecialDivisionsByGender === true) || stacker.customDivision || "Open";
  return stacker;
}

function runtimeStackerToSql(stacker) {
  return {
    stackerCode: stacker.id,
    wssaId: null,
    firstName: stacker.name,
    lastName: "-",
    gender: stacker.gender,
    birthDate: stacker.dob || null,
    country: stacker.country,
    club: stacker.org === "Independent" ? null : stacker.org,
    region: stacker.region || null,
    email: stacker.email || null,
    phone: stacker.phone || null,
    customDivision: stacker.customDivision || null,
    paid: stacker.paid || "No",
    checkedIn: stacker.checkedIn || "No",
    isSpecialStacker: stacker.special === "Yes"
  };
}

async function refreshSqlStackers({ allowEditing = false, rerender = true } = {}) {
  if (!selectedSqlCompetitionId || stackerRefreshInFlight) return false;
  if (editingStackerId && !allowEditing) return false;
  stackerRefreshInFlight = true;
  setSaveStatus("Saving...", "saving");
  try {
    const records = await stackerApi.list(selectedSqlCompetitionId);
    state.stackers = records.map(sqlStackerToRuntime);
    state.divisions = appendStandardImportedDivisions(
      [...generateDivisionNames(state.divisionSettings), ...state.stackers.map(stacker => stacker.division).filter(Boolean), ...state.stackers.map(stacker => stacker.customDivision).filter(Boolean)],
      state.stackers
    );
    refreshDivisionCountBadges(divisionCountSummary(state.divisionSettings));
    if (rerender && route === "stackers") {
      populateStackerAutocompleteOptions();
      drawStackerRows();
    }
    if (rerender && route === "dashboard") {
      renderNav();
      updateSqlDashboardPresentation();
      renderDashboard();
    }
    setSaveStatus("Saved", "saved");
    return true;
  } catch (error) {
    console.error("Unable to refresh SQL-native stackers.", error);
    flashMessage = { type: "error", text: "Save Failed: unable to refresh SQL-native stackers." };
    setSaveStatus("Save Failed", "failed");
    if (rerender && route === "stackers") render();
    return false;
  } finally {
    stackerRefreshInFlight = false;
  }
}

function defaultCompetitionCode() {
  const sessionKey = window.StackMeetAuth?.competitionId?.() || window.COMPETITION_KEY || "DEFAULT";
  const normalized = String(sessionKey).trim().toUpperCase().replace(/[^A-Z0-9_-]/g, "-");
  if (/^[A-Z0-9][A-Z0-9_-]{2,49}$/.test(normalized)) return normalized;
  const year = todayIsoDate().slice(0, 4);
  return `${year}-LOCAL`;
}

function defaultCompetitionVenue() {
  return state.settings.venue || brandText("defaultCompetitionName") || "Local Venue";
}

function sqlDashboardCompetition() {
  return sqlCompetition || {
    competitionCode: defaultCompetitionCode(),
    competitionName: state.settings.name,
    startDate: state.settings.start,
    endDate: state.settings.end,
    venue: "",
    status: state.settings.type
  };
}

function publicResultsUrl() {
  const competition = sqlDashboardCompetition();
  const publicId = competition.competitionCode || window.StackMeetAuth?.competitionId?.() || window.COMPETITION_KEY || "DEFAULT";
  // Public result links use the canonical NADITrack domain, including when officials work locally.
  return `https://naditrack.com/${encodeURIComponent(String(publicId))}/Results`;
}

function qrCodeUrl(value) {
  return `https://qrcodecat.com/api/qrcode?size=300x300&format=png&margin=10&color=0f172a&bgcolor=ffffff&data=${encodeURIComponent(value)}`;
}

function updateSqlDashboardPresentation() {
  const competition = sqlDashboardCompetition();
  const title = document.getElementById("heroEventTitle");
  if (title) title.textContent = competition.competitionName || state.settings.name;
  const context = document.getElementById("competitionType");
  if (context) {
    context.textContent = "";
    context.hidden = true;
  }
}

function applyBrandingChrome() {
  document.title = brandText("browserTitle");
  document.querySelectorAll("[data-brand]").forEach(node => {
    const value = brandText(node.dataset.brand);
    if (value) node.textContent = value;
  });
}

function syncDashboardSqlPolling() {
  if (dashboardPollTimer) {
    clearInterval(dashboardPollTimer);
    dashboardPollTimer = null;
  }
  if (route !== "dashboard" || !selectedSqlCompetitionId) return;
  dashboardPollTimer = setInterval(() => {
    if (route === "dashboard") void refreshSqlStackers({ rerender: true });
  }, 5000);
}

async function createSqlCompetition() {
  const request = {
    competitionCode: val("sqlCompetitionCode").trim(),
    competitionName: val("sqlCompetitionName").trim(),
    venue: val("sqlCompetitionVenue").trim(),
    startDate: val("sqlCompetitionStart"),
    endDate: val("sqlCompetitionEnd"),
    status: val("sqlCompetitionStatus").trim()
  };
  if (!request.competitionCode || !request.competitionName || !request.venue || !request.startDate || !request.endDate || !request.status) {
    flashMessage = { type: "error", text: "Complete the SQL competition setup fields first." };
    return;
  }
  setSaveStatus("Saving...", "saving");
  try {
    const competition = await stackerApi.createCompetition(request);
    setSelectedSqlCompetition(competition);
    await refreshSqlStackers({ allowEditing: true, rerender: false });
    flashMessage = { type: "success", text: `${competition.competitionName} is ready for SQL-native registrations.` };
    setSaveStatus("Saved", "saved");
  } catch (error) {
    flashMessage = { type: "error", text: `Save Failed: ${error.message}` };
    setSaveStatus("Save Failed", "failed");
  }
}

function renderNav() {
  const visibleRoutes = routes.filter(routeIsAvailable);
  document.getElementById("nav").innerHTML = visibleRoutes.map(([key, label, badge]) => {
    const badgeText = navBadgeText(badge);
    return `
      <a href="#${key}" class="nav-item ${navRouteIsActive(key) ? "active" : ""}" data-route="${key}">
        <span>${esc(t(label))}</span>${badgeText ? `<small>${esc(badgeText)}</small>` : ""}
      </a>
    `;
  }).join("");
}

function navRouteIsActive(key) {
  if (key === "reports") return route === "reports" || route === "paperwork";
  if (key === "stackers") return ["stackers", "doubles", "relay"].includes(route);
  return route === key;
}

function navBadgeText(badge) {
  if (!badge) return "";
  if (badge === "__languageBadge") return languageLabel(currentLanguage());
  if (badge === "__languageList") return "English / Bahasa Malaysia / Simplified Chinese";
  if (badge === "__stackerCount") return selectedSqlCompetitionId ? String(state.stackers.length) : "--";
  return t(badge);
}

function routeIsAvailable([key]) {
  return true;
}

function relayTeamSetupAvailable() {
  return eventGroupEnabled("Timed Relay") || eventGroupEnabled("Head To Head");
}

function eventGroupEnabled(group) {
  return (state.events?.[group] || []).length > 0;
}

function render() {
  if (route !== "leaderboard") stopLeaderboardLoop();
  if (!routeIsAvailable([route])) route = "dashboard";
  document.body.classList.toggle("leaderboard-mode", route === "leaderboard");
  applyBrandingChrome();
  renderNav();
  pageTitle.textContent = t(routeTitle(route));
  updateSqlDashboardPresentation();
  translateChrome();
  hero.classList.toggle("hidden", route !== "dashboard");
  const tpl = document.getElementById(`${route}View`) || document.getElementById("dashboardView");
  view.innerHTML = "";
  view.appendChild(tpl.content.cloneNode(true));
  applyEventMenuVisibility();
  const renderers = {
    dashboard: renderDashboard,
    settings: renderSettings,
    language: renderLanguage,
    stackers: renderStackers,
    doubles: renderDoubles,
    relay: renderRelay,
    awards: renderAwards,
    paperwork: renderPaperwork,
    competition: renderCompetition,
    reports: renderReports,
    leaderboard: renderLeaderboard,
    users: renderUsers
  };
  renderers[route]?.();
  syncModuleTabs();
  applyTranslations(view);
  syncDashboardSqlPolling();
  if (route === "dashboard" && selectedSqlCompetitionId) void refreshSqlStackers({ rerender: true });
}

function syncModuleTabs() {
  document.querySelectorAll("[data-participant-route]").forEach(button => {
    button.classList.toggle("active", button.dataset.participantRoute === route);
  });
  document.querySelectorAll("[data-report-route]").forEach(button => {
    const isReportsTab = button.dataset.reportRoute === "reports" && route === "reports" && (!button.dataset.reportTab || button.dataset.reportTab === reportTab);
    const isPaperworkTab = button.dataset.reportRoute === "paperwork" && route === "paperwork";
    button.classList.toggle("active", isReportsTab || isPaperworkTab);
  });
}

function applyEventMenuVisibility() {
  pruneSelectOptions("competitionTypeReport", {
    d: eventGroupEnabled("Doubles"),
    r: eventGroupEnabled("Timed Relay")
  });
  pruneSelectOptions("reportType", {
    doubles: eventGroupEnabled("Doubles"),
    "timed-relay": eventGroupEnabled("Timed Relay")
  });
  pruneSelectOptions("reportTeam", {
    "not-doubles": eventGroupEnabled("Doubles"),
    "not-relay": eventGroupEnabled("Timed Relay")
  });
  pruneSelectOptions("commonReport", {
    all_doubles: eventGroupEnabled("Doubles"),
    not_doubles: eventGroupEnabled("Doubles"),
    all_relay: eventGroupEnabled("Timed Relay"),
    not_relay: eventGroupEnabled("Timed Relay")
  });
  pruneSelectOptions("competitionReportPreset", Object.fromEntries([...document.querySelectorAll("#competitionReportPreset option")]
    .map(option => [option.value, !option.value.includes("doubles") && !option.value.includes("relay") || (option.value.includes("doubles") && eventGroupEnabled("Doubles")) || (option.value.includes("relay") && eventGroupEnabled("Timed Relay"))])));
  pruneSelectOptions("bracketType", { Relay: eventGroupEnabled("Timed Relay") });
}

function pruneSelectOptions(id, availability) {
  const select = document.getElementById(id);
  if (!select) return;
  [...select.options].forEach(option => {
    if (option.value in availability && !availability[option.value]) option.remove();
  });
}

function currentLanguage() {
  return state.settings?.language || "en";
}

function languageLabel(code) {
  if (code === "ms") return "Bahasa Malaysia";
  if (code === "zh") return "Simplified Chinese";
  return "English";
}

function t(text) {
  const code = currentLanguage();
  if (code === "en") return text;
  return state.translations?.[code]?.[text] || text;
}

function translateChrome() {
  document.getElementById("exportXmlBtn")?.setAttribute("aria-label", t("Export XML"));
  document.querySelector("label[for='importXmlInput']")?.setAttribute("aria-label", t("Import XML"));
  const resetButton = document.getElementById("resetBtn");
  if (resetButton) resetButton.textContent = t("Reset Competition");
  document.querySelector(".sidebar-card span").textContent = t("Local mode");
  document.querySelector(".sidebar-card strong").textContent = t("Saved in this browser");
}

function applyTranslations(root) {
  const code = currentLanguage();
  if (code === "en" || !root) return;
  const skipTags = new Set(["SCRIPT", "STYLE", "INPUT", "TEXTAREA", "SELECT", "OPTION"]);
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const nodes = [];
  while (walker.nextNode()) nodes.push(walker.currentNode);
  nodes.forEach(node => {
    const parent = node.parentElement;
    if (!parent || skipTags.has(parent.tagName)) return;
    if (parent.closest(".no-auto-translate")) return;
    const raw = node.nodeValue;
    const trimmed = raw.trim();
    const translated = state.translations?.[code]?.[trimmed];
    if (!trimmed || !translated) return;
    node.nodeValue = raw.replace(trimmed, translated);
  });
}

function renderDashboard() {
  const competition = sqlDashboardCompetition();
  const resultsUrl = publicResultsUrl();
  const metrics = {
    stackers: state.stackers.length,
    gender: `${countBy("gender", "F")} Female // ${countBy("gender", "M")} Male`,
    doubles: state.doubles.length,
    relay: completedRelays().length,
    divisions: state.divisions.length,
    fastest: fmt(Math.min(...state.results.map(official).filter(Number.isFinite)))
  };
  Object.entries(metrics).forEach(([key, value]) => {
    const el = document.querySelector(`[data-metric="${key}"]`);
    if (el) el.textContent = value;
  });
  document.getElementById("snapshot").innerHTML = `
    <div class="list">
      <div class="list-row"><span>Name</span><strong>${esc(competition.competitionName)}</strong></div>
      <div class="list-row"><span>Date</span><strong>${esc(competition.startDate)} to ${esc(competition.endDate)}</strong></div>
      <div class="list-row"><span>Venue</span><strong>${esc(competition.venue || "--")}</strong></div>
      <div class="list-row"><span>Rounds</span><strong>${state.settings.prelims} prelim / ${state.settings.finals} final</strong></div>
      <div class="list-row"><span>Version</span><strong>${esc(STACKMEET_APP_VERSION)}</strong></div>
    </div>
    <div class="results-share no-auto-translate">
      <div>
        <span>Public Results</span>
        <a href="${esc(resultsUrl)}" target="_blank" rel="noopener">${esc(resultsUrl)}</a>
      </div>
      <img src="${esc(qrCodeUrl(resultsUrl))}" alt="QR code for public results" loading="lazy" />
    </div>
  `;
}

function routeTitle(key) {
  if (["stackers", "doubles", "relay"].includes(key)) return "Participant";
  if (key === "paperwork") return "Reports";
  return routes.find(([routeKey]) => routeKey === key)?.[1] || "Dashboard";
}

function renderNotifications() {
  const unread = state.notifications.filter(n => !n.read).length;
  document.getElementById("notificationList").innerHTML = `
    <div class="list">
      ${state.notifications.map(n => `<div class="list-row"><span><strong>${esc(n.title)}</strong><br><small>${esc(n.time)}</small></span><span class="pill ${n.read ? "" : "warning"}">${n.read ? "Read" : "New"}</span></div>`).join("")}
      <div class="list-row"><span>Unread</span><strong>${unread}</strong></div>
    </div>
  `;
}

function renderSettings() {
  setValue("settingName", state.settings.name);
  setValue("settingType", state.settings.type);
  setValue("settingStart", state.settings.start);
  setValue("settingEnd", state.settings.end);
  setValue("settingKbsLogo", state.settings.kbsLogo);
  setValue("settingPrelims", state.settings.prelims);
  setValue("settingFinals", state.settings.finals);
  setValue("settingSoc", state.settings.soc);
  setValue("settingPrelimTimes", state.settings.prelimTimes);
  setValue("settingPaperless", state.settings.paperless);
  setValue("settingLanguage", state.settings.language || "en");
  setValue("settingAgeCalculation", state.settings.ageCalculationMode === "yearBorn" ? "yearBorn" : "actual");
  const separateSpecialGender = document.getElementById("settingSeparateSpecialGender");
  if (separateSpecialGender) separateSpecialGender.checked = state.settings.separateSpecialDivisionsByGender === true;
  setValue("settingAdvanceIndividuals", state.settings.advanceIndividuals);
  setValue("settingAdvanceDoubles", state.settings.advanceDoubles);
  setValue("settingAdvanceCpDoubles", state.settings.advanceCpDoubles);
  setValue("settingAdvanceRelay", state.settings.advanceRelay);
  setValue("settingTimeSheetInput", state.settings.timeSheetInput);
  setValue("leaderType", state.leaderboard.type);
  setValue("leaderStage", state.leaderboard.stage);
  setValue("leaderFontSize", state.leaderboard.fontSize);
  setValue("leaderBg", state.leaderboard.bg);
  setValue("leaderColor", state.leaderboard.color);
  setValue("leaderProgressHeight", state.leaderboard.progressHeight);
  setValue("leaderPause", state.leaderboard.pause);
  setValue("leaderLimit", state.leaderboard.limit);

  document.getElementById("eventMatrix").innerHTML = Object.entries(eventGroups).map(([group, events]) => `
    <article class="check-card">
      <h3>${esc(group)}</h3>
      ${events.map((event, index) => {
        const singleSelection = ["Doubles", "Timed Relay"].includes(group);
        const inputType = singleSelection ? "radio" : "checkbox";
        const inputName = singleSelection ? `event-${group.replace(/\s+/g, "-").toLowerCase()}` : "";
        const checked = singleSelection
          ? state.events[group]?.[0] === event
          : state.events[group]?.includes(event);
        return `<label><input type="${inputType}" ${inputName ? `name="${inputName}"` : ""} data-event-group="${esc(group)}" value="${esc(event)}" ${checked ? "checked" : ""}> ${esc(event)}</label>`;
      }).join("")}
    </article>
  `).join("");

  renderDivisionCutoffs();

  document.getElementById("divisionList").innerHTML = sortedDivisions(state.divisions).map(division => `
    <div class="tag-row"><strong>${esc(division)}</strong><button class="icon-button" data-action="remove-division" data-division="${esc(division)}" type="button">x</button></div>
  `).join("");
  renderCompetitionAuditLogs();
}

// Renders the latest per-competition audit entries in the Settings page.
function renderCompetitionAuditLogs() {
  const body = document.getElementById("competitionAuditRows");
  if (!body) return;
  const logs = [...(state.auditLogs || [])].sort((left, right) => String(right.atUtc).localeCompare(String(left.atUtc))).slice(0, 200);
  if (!logs.length) {
    body.innerHTML = '<tr><td colspan="5" class="muted">No competition audit logs yet.</td></tr>';
    return;
  }

  body.innerHTML = logs.map(log => `
    <tr>
      <td>${esc(stackMeetDateTime(parseUtcDate(log.atUtc)))}</td>
      <td>${esc(log.action)}</td>
      <td>${esc(log.actorEmail || log.actorName || "Competition user")}</td>
      <td>${esc([log.entityType, log.entityId].filter(Boolean).join(" #"))}</td>
      <td>${esc(log.summary || auditChangeSummary(log))}</td>
    </tr>
  `).join("");
}

// Exports the per-competition audit trail with MYT display time and raw UTC time.
function exportCompetitionAuditCsv() {
  const rows = (state.auditLogs || []).map(log => [
    stackMeetDateTime(parseUtcDate(log.atUtc)),
    log.atUtc,
    log.action,
    log.actorEmail || "",
    log.actorName || "",
    log.entityType || "",
    log.entityId || "",
    log.summary || "",
    JSON.stringify(log.before ?? null),
    JSON.stringify(log.after ?? null)
  ]);
  const header = ["Time (MYT)", "Time (UTC)", "Action", "Actor Email", "Actor Name", "Entity Type", "Entity ID", "Summary", "Before JSON", "After JSON"];
  const generated = [["Competition Audit Logs"], [state.settings.name || currentCompetitionKey()], [`Exported ${stackMeetDateTime()}`], []];
  downloadText(`NADITrack-audit-${currentCompetitionKey()}.csv`, [...generated, header, ...rows].map(csvLine).join("\n"), "text/csv");
}

// Provides a compact fallback summary when an audit entry has before/after snapshots only.
function auditChangeSummary(log) {
  if (log.before && log.after) return "Updated";
  if (log.after) return "Created";
  if (log.before) return "Removed";
  return "";
}

function renderLanguage() {
  setValue("languageActive", state.settings.language || "en");
  document.getElementById("languageActive")?.addEventListener("change", drawLanguageRows);
  drawLanguageRows();
  document.getElementById("languageSearch")?.addEventListener("input", drawLanguageRows);
}

function drawLanguageRows() {
  const code = val("languageActive") === "zh" ? "zh" : "ms";
  const label = languageLabel(code);
  const term = (document.getElementById("languageSearch")?.value || "").toLowerCase();
  const entries = Object.entries(state.translations[code] || {})
    .filter(([english, translated]) => !term || `${english} ${translated}`.toLowerCase().includes(term))
    .sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true, sensitivity: "base" }));
  const head = document.querySelector(".language-table thead tr");
  if (head) head.innerHTML = `<th>English</th><th>${esc(label)}</th>`;
  document.getElementById("languageRows").innerHTML = entries.map(([english, malay]) => `
    <tr>
      <td><strong>${esc(english)}</strong></td>
      <td><input data-language-code="${esc(code)}" data-language-key="${esc(english)}" value="${esc(malay)}" /></td>
    </tr>
  `).join("");
}

function renderDivisionCutoffs() {
  const individualGroups = [
    ["combined", "Combined", "C"],
    ["male", "Male", "M"],
    ["female", "Female", "F"],
    ["special", "Special Stackers", "SS L1"]
  ];
  const doublesGroups = [
    ["doubles", "Doubles", "U"],
    ["childParentDoubles", "Child/Parent Doubles", "U"],
    ["specialDoubles", "Special Doubles", "SS"],
    ["specialChildParentDoubles", "Special Child/Parent", "SS CP"]
  ];
  const relayGroups = [
    ["timedRelay", "Timed Relay", "U"],
    ["headToHeadRelay", "Head-to-Head Relay", "U"]
  ];
  const counts = divisionCountSummary(state.divisionSettings);
  const renderGroups = groups => groups.map(([key, label, suffix]) => `
    <article class="division-column">
      <h3>${esc(label)}</h3>
      ${divisionAges.map(age => `
        <label class="age-cutoff">
          <input type="checkbox" data-division-group="${key}" value="${age}" ${state.divisionSettings[key]?.includes(age) ? "checked" : ""}>
          <span>${age} ${suffix}</span>
          ${countBadges(key, age, counts)}
        </label>
      `).join("")}
    </article>
  `).join("");
  const individualContainer = document.getElementById("divisionCutoffs");
  const doublesContainer = document.getElementById("doublesDivisionCutoffs");
  const relayContainer = document.getElementById("relayDivisionCutoffs");
  if (individualContainer) individualContainer.innerHTML = renderGroups(individualGroups);
  if (doublesContainer) doublesContainer.innerHTML = renderGroups(doublesGroups);
  if (relayContainer) relayContainer.innerHTML = renderGroups(relayGroups);

  document.querySelectorAll("[data-division-group]").forEach(input => {
    input.addEventListener("change", () => {
      updateDivisionSettingsFromForm({ recalculateEntries: false });
      const previewCounts = divisionCountSummary(state.divisionSettings);
      refreshDivisionCountBadges(previewCounts);
      document.getElementById("divisionList").innerHTML = state.divisions.map(division => `
        <div class="tag-row"><strong>${esc(division)}</strong></div>
      `).join("");
    });
  });
}

function countBadges(group, age, counts) {
  const exact = counts.exact[group]?.[age] || 0;
  const grouped = counts.grouped[group]?.[age] || 0;
  return `<span class="age-counts" data-count-group="${group}" data-count-age="${age}">
    ${exact ? `<span class="count-badge muted-count">${exact}</span>` : ""}
    ${grouped ? `<span class="count-badge blue-count">${grouped}</span>` : ""}
  </span>`;
}

function refreshDivisionCountBadges(counts) {
  document.querySelectorAll("[data-count-group][data-count-age]").forEach(container => {
    const group = container.dataset.countGroup;
    const age = Number(container.dataset.countAge);
    const exact = counts.exact[group]?.[age] || 0;
    const grouped = counts.grouped[group]?.[age] || 0;
    container.innerHTML = `${exact ? `<span class="count-badge muted-count">${exact}</span>` : ""}${grouped ? `<span class="count-badge blue-count">${grouped}</span>` : ""}`;
  });
}

function divisionCountSummary(settings) {
  const exact = {
    combined: {},
    male: {},
    female: {},
    special: {}
  };
  const grouped = {
    combined: {},
    male: {},
    female: {},
    special: {}
  };

  state.stackers.forEach(stacker => {
    const age = ageOnCompetitionDate(stacker.dob, state.settings.start) || Number(stacker.age);
    if (!Number.isFinite(age) || age <= 0) return;

    const isSpecial = stacker.special === "Yes";
    const genderGroup = stacker.gender === "F" ? "female" : "male";
    exact.combined[age] = (exact.combined[age] || 0) + 1;
    exact[genderGroup][age] = (exact[genderGroup][age] || 0) + 1;
    if (isSpecial) exact.special[age] = (exact.special[age] || 0) + 1;

  });

  state.stackers.forEach(stacker => {
    const age = ageOnCompetitionDate(stacker.dob, state.settings.start) || Number(stacker.age);
    if (!Number.isFinite(age) || age <= 0) return;
    const target = divisionTargetForAge(age, stacker.gender, stacker.special === "Yes", settings);
    if (target) grouped[target.group][target.cutoff] = (grouped[target.group][target.cutoff] || 0) + 1;
  });

  return { exact, grouped };
}

function divisionTargetForAge(age, gender, special = false, settings = state.divisionSettings) {
  if (special) return cutoffTarget(age, settings.special || [], "special");
  const genderGroup = gender === "F" ? "female" : "male";
  const genderLabel = gender === "F" ? "Female" : "Male";
  const target = divisionPath(settings, genderGroup, genderLabel).find(item => age <= item.age);
  if (!target) return null;
  return {
    group: target.label === "Combined" ? "combined" : genderGroup,
    cutoff: target.age
  };
}

function cutoffTarget(age, cutoffs, group) {
  const cutoff = [...cutoffs].sort((a, b) => a - b).find(value => age <= value);
  return cutoff ? { group, cutoff } : null;
}

function renderStackers() {
  const setup = document.getElementById("sqlCompetitionSetup");
  if (setup) setup.hidden = Boolean(selectedSqlCompetitionId);
  if (!selectedSqlCompetitionId) {
    setValue("sqlCompetitionName", state.settings.name || "");
    if (!val("sqlCompetitionCode")) setValue("sqlCompetitionCode", defaultCompetitionCode());
    if (!val("sqlCompetitionVenue")) setValue("sqlCompetitionVenue", defaultCompetitionVenue());
    const startDate = isoDateValue(state.settings.start) || todayIsoDate();
    setValue("sqlCompetitionStart", startDate);
    setValue("sqlCompetitionEnd", isoDateValue(state.settings.end) || startDate);
  }
  setOptions("stCountry", countries);
  setValue("stCountry", "Malaysia");
  const form = document.getElementById("stackerForm");
  if (form) form.hidden = !stackerFormVisible;
  const showFormButton = document.getElementById("showStackerFormBtn");
  if (showFormButton) showFormButton.hidden = stackerFormVisible;
  setOptions("stackerDivisionFilter", ["All Divisions", ...sortedDivisions(state.divisions)]);
  populateStackerAutocompleteOptions();
  syncStackerEditState();
  if (flashMessage) {
    const box = document.getElementById("stackerMessage");
    box.hidden = false;
    box.textContent = flashMessage.text;
    box.classList.toggle("error", flashMessage.type === "error");
    flashMessage = null;
  }
  drawStackerRows();
  document.getElementById("stackerSearch").addEventListener("input", drawStackerRows);
  document.getElementById("stackerDivisionFilter").addEventListener("change", drawStackerRows);
  document.getElementById("stackerCsvInput")?.addEventListener("change", importStackersCsvFile);
  ["stDob", "stGender", "stSpecial", "stCustomDivision"].forEach(id => {
    document.getElementById(id)?.addEventListener("input", updateStackerDivisionPreview);
    document.getElementById(id)?.addEventListener("change", updateStackerDivisionPreview);
  });
  updateStackerDivisionPreview();
  if (selectedSqlCompetitionId && !editingStackerId) void refreshSqlStackers({ rerender: true });
  if (focusStackerListAfterRender) {
    focusStackerListAfterRender = false;
    document.getElementById("stackerListHeading")?.focus();
  }
}

function populateStackerAutocompleteOptions() {
  setDatalistOptions("customDivisionOptions", [
    ...(state.divisionSettings?.custom || []),
    ...state.stackers.map(stacker => stacker.customDivision).filter(Boolean)
  ]);
  setDatalistOptions("organizationOptions", state.stackers.map(stacker => stacker.org).filter(Boolean));
  setDatalistOptions("regionOptions", state.stackers.map(stacker => stacker.region).filter(Boolean));
}

function setDatalistOptions(id, options) {
  const datalist = document.getElementById(id);
  if (!datalist) return;
  datalist.innerHTML = [...new Set(options.map(option => String(option || "").trim()).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" }))
    .map(option => `<option value="${esc(option)}"></option>`)
    .join("");
}

function drawStackerRows() {
  const search = document.getElementById("stackerSearch")?.value.toLowerCase() || "";
  const division = document.getElementById("stackerDivisionFilter")?.value || "All Divisions";
  const rows = sortStackers(state.stackers.filter(s => {
    const matchesSearch = `${s.id} ${s.name} ${s.org} ${s.division}`.toLowerCase().includes(search);
    const matchesDivision = division === "All Divisions" || s.division === division;
    return matchesSearch && matchesDivision;
  }));
  updateSortHeaders();
  document.getElementById("stackerRows").innerHTML = rows.map(s => `
    <tr>
      <td><button class="link-button" data-action="edit-stacker" data-id="${esc(s.id)}" type="button">${esc(s.id)}</button></td>
      <td><button class="link-button" data-action="edit-stacker" data-id="${esc(s.id)}" type="button">${esc(s.name)}</button></td>
      <td>${esc(s.age || ageOnCompetitionDate(s.dob, state.settings.start))}</td><td>${esc(s.gender)}</td><td><span class="pill ${s.special === "Yes" ? "blue" : ""}">${esc(s.special || "No")}</span></td><td>${esc(s.org)}</td><td>${esc(s.division)}</td><td>${esc(s.country)}</td>
      <td><span class="pill ${s.paid === "Yes" ? "" : "warning"}">${esc(s.paid)}</span></td>
      <td><span class="pill ${s.checkedIn === "Yes" ? "blue" : "warning"}">${esc(s.checkedIn || "No")}</span></td>
      <td><button class="icon-button" data-action="delete-stacker" data-id="${esc(s.id)}" type="button">x</button></td>
    </tr>
  `).join("");
}

function sortStackers(rows) {
  const direction = stackerSort.direction === "desc" ? -1 : 1;
  return [...rows].sort((a, b) => compareStackers(a, b, stackerSort.key) * direction);
}

function compareStackers(a, b, key) {
  if (key === "id") return stackerIdNumber(a.id) - stackerIdNumber(b.id);
  if (key === "age") return Number(a.age || 0) - Number(b.age || 0);
  return String(a[key] || "").localeCompare(String(b[key] || ""), undefined, { numeric: true, sensitivity: "base" });
}

function stackerIdNumber(id) {
  const match = /^(\d+)\.(\d+)$/.exec(String(id || ""));
  return match ? Number(match[1]) * 100000 + Number(match[2]) : Number.MAX_SAFE_INTEGER;
}

function updateSortHeaders() {
  document.querySelectorAll("[data-sort-key]").forEach(button => {
    const active = button.dataset.sortKey === stackerSort.key;
    const label = button.textContent.replace(/\s+[\^v]$/, "");
    button.textContent = active ? `${label} ${stackerSort.direction === "asc" ? "^" : "v"}` : label;
  });
}

function sortStackerTable(key) {
  if (!key) return;
  if (stackerSort.key === key) {
    stackerSort.direction = stackerSort.direction === "asc" ? "desc" : "asc";
  } else {
    stackerSort = { key, direction: "asc" };
  }
  drawStackerRows();
}

function renderDoubles() {
  populateDoubleSelects();
  syncDoubleEditState();
  showDoubleMessage();
  updateDoubleFormMode();
  document.querySelectorAll("[data-doubles-tab]").forEach(button => {
    button.classList.toggle("active", button.dataset.doublesTab === doublesTab);
  });
  const rows = filteredDoublesForTab();
  document.getElementById("doubleRows").innerHTML = rows.map(d => `
    <tr>
      <td>${esc(d.id)}</td>
      <td><strong>${esc(participantName("Doubles", d.id))}</strong></td>
      <td>${esc(doubleTypeLabel(d))}</td>
      <td><span class="pill ${d.status === "pending" ? "warning" : "blue"}">${esc(doubleStatusLabel(d))}</span></td>
      <td>${esc(doubleDivision(d))}</td>
      <td>${esc(d.country || teamCountry(d))}</td>
      <td><div class="button-row compact-actions"><button class="ghost compact-button" data-action="edit-double" data-id="${esc(d.id)}" type="button">Edit</button><button class="icon-button" data-action="delete-double" data-id="${esc(d.id)}" type="button">x</button></div></td>
    </tr>
  `).join("") || `<tr><td colspan="7"><span class="muted">No doubles found for this tab.</span></td></tr>`;
  ["doubleOneSearch", "doubleTwoSearch"].forEach(id => document.getElementById(id)?.addEventListener("input", populateDoubleSelects));
  ["doubleOne", "doubleTwo", "doubleStatus", "doubleParentName"].forEach(id => {
    document.getElementById(id)?.addEventListener("input", updateDoubleFormMode);
    document.getElementById(id)?.addEventListener("change", updateDoubleFormMode);
  });
}

function syncDoubleEditState() {
  const saveButton = document.getElementById("saveDoubleBtn");
  const cancelButton = document.getElementById("cancelDoubleEdit");
  if (saveButton) saveButton.textContent = editingDoubleId ? `Update ${editingDoubleId}` : "Add Team";
  if (cancelButton) cancelButton.hidden = !editingDoubleId;
}

function populateDoubleSelects() {
  const oneCurrent = selectedStackerId("doubleOne");
  const twoCurrent = selectedStackerId("doubleTwo");
  fillDoubleSelect("doubleOne", val("doubleOneSearch"), oneCurrent);
  fillDoubleSelect("doubleTwo", val("doubleTwoSearch"), twoCurrent);
}

function fillDoubleSelect(id, search, selectedId = "") {
  const term = String(search || "").trim().toLowerCase();
  const options = state.stackers
    .filter(stacker => !term || stackerPickerSearchText(stacker).includes(term))
    .sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  const html = [`<option value="">--</option>`].concat(options.map(stacker => {
    const team = doublesForStacker(stacker.id).find(existing => existing.id !== editingDoubleId);
    const className = team ? "assigned-option" : "";
    const status = team ? `Team ${team.id}: ${participantName("Doubles", team.id)}` : "Available";
    return `<option value="${esc(stacker.id)}" class="${className}">${esc(stackerPickerLabel(stacker, status))}</option>`;
  })).join("");
  const select = document.getElementById(id);
  if (!select) return;
  select.innerHTML = html;
  if (selectedId && [...select.options].some(option => option.value === selectedId)) select.value = selectedId;
}

function filteredDoublesForTab() {
  const sorted = [...state.doubles].sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  if (doublesTab === "completed") return sorted.filter(team => team.status !== "pending");
  if (doublesTab === "incomplete") return sorted.filter(team => team.status === "pending");
  return sorted;
}

function completedDoubles() {
  return state.doubles.filter(team => team.status !== "pending");
}

function renderRelay() {
  buildRelayMemberControls();
  populateRelaySelects();
  syncRelayEditState();
  showRelayMessage();
  document.querySelectorAll("[data-relay-tab]").forEach(button => {
    button.classList.toggle("active", button.dataset.relayTab === relayTab);
  });
  const rows = filteredRelaysForTab();
  document.getElementById("relayRows").innerHTML = rows.map(team => {
    const members = relayMemberIds(team);
    const status = relayTeamStatus(team);
    return `<tr>
      <td>${esc(team.id)}</td>
      <td><strong>${esc(participantName("Timed Relay", team.id))}</strong>${team.coordinator ? `<br><small>${esc(team.coordinator)}</small>` : ""}</td>
      <td>${esc(relayTimedDivision(team))}</td>
      <td>${esc(relayHeadToHeadDivision(team))}</td>
      <td>${esc(members.map(stackerName).join(" / ") || "--")}</td>
      <td><strong>${members.length}</strong></td>
      <td><span class="pill ${relayStatusClass(status)}">${esc(status)}</span></td>
      <td>${esc(relayLocation(team))}</td>
      <td><div class="button-row compact-actions"><button class="ghost compact-button" data-action="edit-relay" data-id="${esc(team.id)}" type="button">Edit</button><button class="icon-button" data-action="delete-relay" data-id="${esc(team.id)}" type="button">x</button></div></td>
    </tr>`;
  }).join("") || `<tr><td colspan="9"><span class="muted">No relay teams found for this tab.</span></td></tr>`;
  document.querySelectorAll("[data-relay-search]").forEach(input => input.addEventListener("input", populateRelaySelects));
  document.querySelectorAll("[data-relay-member]").forEach(select => select.addEventListener("change", showSelectedRelayWarnings));
}

function buildRelayMemberControls() {
  const grid = document.getElementById("relayMemberGrid");
  if (!grid) return;
  grid.innerHTML = Array.from({ length: 6 }, (_, index) => {
    const slot = index + 1;
    const memberLabel = slot <= 4 ? `Member ${slot}` : `Optional Member ${slot}`;
    return `<label>Search ${memberLabel}<input id="relayMemberSearch${slot}" data-relay-search="${slot}" placeholder="Name or stacker ID" /></label>
      <label>${memberLabel}<select id="relayMember${slot}" data-relay-member="${slot}"></select></label>`;
  }).join("");
}

function syncRelayEditState() {
  const saveButton = document.getElementById("saveRelayBtn");
  const cancelButton = document.getElementById("cancelRelayEdit");
  if (saveButton) saveButton.textContent = editingRelayId ? `Update ${editingRelayId}` : "Add Team";
  if (cancelButton) cancelButton.hidden = !editingRelayId;
  if (saveButton) saveButton.disabled = false;
  document.querySelectorAll("#relayMemberGrid input, #relayMemberGrid select, #relayName, #timedRelayDivision, #headToHeadDivision, #relayCoordinator, #relayEmail, #relayPhone, #relayRegion").forEach(control => { control.disabled = false; });
}

function showRelayMessage() {
  const box = document.getElementById("relayMessage");
  if (!box) return;
  if (!relayFlashMessage) {
    box.hidden = true;
    return;
  }
  box.hidden = false;
  box.textContent = relayFlashMessage.text;
  box.classList.toggle("error", relayFlashMessage.type === "error");
  relayFlashMessage = null;
}

function populateRelaySelects() {
  for (let slot = 1; slot <= 6; slot += 1) {
    fillRelaySelect(slot, val(`relayMemberSearch${slot}`), selectedStackerId(`relayMember${slot}`));
  }
  showSelectedRelayWarnings();
}

function fillRelaySelect(slot, search, selectedId = "") {
  const select = document.getElementById(`relayMember${slot}`);
  if (!select) return;
  const selectedInOtherSlots = selectedRelayMemberIds().filter((id, index) => id && index !== slot - 1);
  const term = String(search || "").trim().toLowerCase();
  const options = state.stackers
    .filter(stacker => !term || stackerPickerSearchText(stacker).includes(term))
    .sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  select.innerHTML = [`<option value="">--</option>`].concat(options.map(stacker => {
    const assigned = relayForStacker(stacker.id);
    const duplicate = selectedInOtherSlots.includes(stacker.id);
    const className = assigned && assigned.id !== editingRelayId ? "assigned-option" : "";
    const status = duplicate ? "Already selected here" : assigned && assigned.id !== editingRelayId ? `Team ${assigned.id}: ${participantName("Timed Relay", assigned.id)}` : "Available";
    return `<option value="${esc(stacker.id)}" class="${className}" ${duplicate ? "disabled" : ""}>${esc(stackerPickerLabel(stacker, status))}</option>`;
  })).join("");
  if (selectedId && [...select.options].some(option => option.value === selectedId)) select.value = selectedId;
}

function selectedRelayMemberIds() {
  return Array.from({ length: 6 }, (_, index) => selectedStackerId(`relayMember${index + 1}`));
}

function showSelectedRelayWarnings() {
  const box = document.getElementById("relayWarning");
  if (!box) return;
  const selected = selectedRelayMemberIds().filter(Boolean);
  const duplicates = selected.filter((id, index) => selected.indexOf(id) !== index);
  const conflicts = selected
    .map(id => ({ id, team: relayForStacker(id) }))
    .filter(item => item.team && item.team.id !== editingRelayId);
  const messages = [];
  if (selected.length > 0 && selected.length < 4) messages.push("Incomplete Team: Minimum 4 registered stackers are required before this team may compete.");
  if (duplicates.length) messages.push("Each stacker can only be selected once in this relay team.");
  if (conflicts.length) messages.push(`${conflicts.map(item => `${stackerName(item.id)} is now in ${item.team.id}`).join("; ")}. Saving will remove them from the current relay team.`);
  if (!messages.length) {
    box.hidden = true;
    box.textContent = "";
    return;
  }
  box.hidden = false;
  box.textContent = messages.join(" ");
}

function filteredRelaysForTab() {
  const sorted = [...state.relays].sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  if (relayTab === "ready") return sorted.filter(relayCanCompete);
  if (relayTab === "incomplete") return sorted.filter(team => relayTeamStatus(team) === "Incomplete");
  if (relayTab === "draft") return sorted.filter(team => relayTeamStatus(team) === "Draft");
  return sorted;
}

function completedRelays() {
  return state.relays.filter(relayIsComplete);
}

function relayIsComplete(team) {
  return relayCanCompete(team);
}

function relayCanCompete(team) {
  return relayMemberIds(team).length >= 4;
}

function relayTeamStatus(team) {
  const memberCount = relayMemberIds(team).length;
  if (memberCount === 0) return "Draft";
  if (memberCount < 4) return "Incomplete";
  return "Ready";
}

function relayStatusClass(status) {
  if (status === "Ready") return "blue";
  return "warning";
}

function renderAwards() {
  state.awards = normalizeAwards(state.awards);
  fillAwardLimitSelect("awardIndividualPlaces", state.awards.individualPlaces);
  fillAwardLimitSelect("awardDoublesPlaces", state.awards.doublesPlaces);
  fillAwardLimitSelect("awardRelayPlaces", state.awards.relayPlaces);
  setValue("awardRelayUnits", state.awards.relayUnits);
  document.getElementById("awardIndividualItems").innerHTML = awardPlaceItemControls("individual", state.awards.individualPlaces, state.awards.individualItems);
  document.getElementById("awardDoublesItems").innerHTML = awardPlaceItemControls("doubles", state.awards.doublesPlaces, state.awards.doublesItems);
  document.getElementById("awardRelayItems").innerHTML = awardPlaceItemControls("relay", state.awards.relayPlaces, state.awards.relayItems);
  document.getElementById("awardOverallConfig").innerHTML = awardOverallGroups.map(group => {
    const config = state.awards.overall[group.key] || defaultAwards.overall[group.key];
    return `<article class="award-config-card">
      <h3>${esc(group.label)}</h3>
      <p class="muted">${esc(group.basis)}</p>
      <label>Places<select id="awardOverallLimit-${esc(group.key)}">${awardLimitOptions(config.limit, true)}</select></label>
      <label>Award Item<select id="awardOverallItem-${esc(group.key)}">${awardItemOptions(config.item)}</select></label>
    </article>`;
  }).join("");
  document.querySelectorAll("[id^='award']").forEach(select => select.addEventListener("change", handleAwardPlannerChange));
  drawAwardSummary();
}

function handleAwardPlannerChange(event) {
  saveAwards(false);
  if (["awardIndividualPlaces", "awardDoublesPlaces", "awardRelayPlaces"].includes(event.target.id)) {
    refreshAwardPlaceControls();
  }
  drawAwardSummary();
}

function refreshAwardPlaceControls() {
  document.getElementById("awardIndividualItems").innerHTML = awardPlaceItemControls("individual", state.awards.individualPlaces, state.awards.individualItems);
  document.getElementById("awardDoublesItems").innerHTML = awardPlaceItemControls("doubles", state.awards.doublesPlaces, state.awards.doublesItems);
  document.getElementById("awardRelayItems").innerHTML = awardPlaceItemControls("relay", state.awards.relayPlaces, state.awards.relayItems);
  document.querySelectorAll("#awardIndividualItems select, #awardDoublesItems select, #awardRelayItems select")
    .forEach(select => select.addEventListener("change", handleAwardPlannerChange));
}

function fillAwardLimitSelect(id, value) {
  const select = document.getElementById(id);
  if (select) select.innerHTML = awardLimitOptions(value);
}

function awardLimitOptions(selected, allowZero = false) {
  const values = allowZero ? [0, 3, 5, 10] : [3, 5, 10];
  return values.map(value => `<option value="${value}" ${Number(selected) === value ? "selected" : ""}>${value ? `Top ${value}` : "No Award"}</option>`).join("");
}

function awardPlaceItemControls(prefix, places, items) {
  return Array.from({ length: Number(places) || 0 }, (_, index) => `
    <label>${esc(ordinal(index + 1))}<select id="award-${prefix}-${index}">${awardItemOptions(items[index])}</select></label>
  `).join("");
}

function awardItemOptions(selected) {
  return ["Trophy", "Medal"].map(item => `<option ${normalizeAwardItem(selected) === item ? "selected" : ""}>${item}</option>`).join("");
}

function saveAwards(renderNow = true) {
  const individualPlaces = Number(val("awardIndividualPlaces")) || state.awards.individualPlaces;
  const doublesPlaces = Number(val("awardDoublesPlaces")) || state.awards.doublesPlaces;
  const relayPlaces = Number(val("awardRelayPlaces")) || state.awards.relayPlaces;
  state.awards = normalizeAwards({
    individualPlaces,
    individualItems: Array.from({ length: individualPlaces }, (_, index) =>
      val(`award-individual-${index}`) || state.awards.individualItems[index] || "Medal"),
    doublesPlaces,
    doublesItems: Array.from({ length: doublesPlaces }, (_, index) =>
      val(`award-doubles-${index}`) || state.awards.doublesItems[index] || "Medal"),
    relayPlaces,
    relayUnits: Number(val("awardRelayUnits")) || state.awards.relayUnits,
    relayItems: Array.from({ length: relayPlaces }, (_, index) =>
      val(`award-relay-${index}`) || state.awards.relayItems[index] || "Medal"),
    overall: Object.fromEntries(awardOverallGroups.map(group => [group.key, {
      limit: Number(val(`awardOverallLimit-${group.key}`)) || 0,
      item: val(`awardOverallItem-${group.key}`) || "Trophy"
    }]))
  });
  if (renderNow) renderAwards();
}

function drawAwardSummary() {
  const rows = awardPlanRows();
  const totals = rows.reduce((acc, row) => {
    acc[row.item] = (acc[row.item] || 0) + row.quantity;
    return acc;
  }, {});
  document.getElementById("awardTotals").innerHTML = ["Trophy", "Medal"].map(item => `
    <article><span>${esc(item)}s Needed</span><strong>${totals[item] || 0}</strong><small>${esc(item.toLowerCase())} inventory</small></article>
  `).join("");
  document.getElementById("awardRows").innerHTML = rows.map(row => `
    <tr><td><strong>${esc(row.group)}</strong></td><td>${esc(row.basis)}</td><td>${esc(row.place)}</td><td>${esc(row.item)}</td><td><strong>${row.quantity}</strong></td></tr>
  `).join("") || `<tr><td colspan="5"><span class="muted">No awards selected.</span></td></tr>`;
}

function awardPlanRows() {
  return [
    ...individualAwardRows(),
    ...doublesAwardRows(),
    ...relayAwardRows(),
    ...overallAwardRows()
  ];
}

function individualAwardRows() {
  const divisions = plannedIndividualAwardDivisions();
  const events = plannedEventsForGroup("Individuals");
  return divisions.flatMap(division => events.flatMap(event => awardRowsForPlaces({
      group: `Individual - ${division}`,
      basis: `Planned division // ${event}`,
      places: Number(state.awards.individualPlaces) || 0,
      items: state.awards.individualItems,
      unitsForPlace: () => 1
    })));
}

function doublesAwardRows() {
  const divisions = plannedDoublesAwardDivisions();
  const events = plannedEventsForGroup("Doubles");
  return divisions.flatMap(division => events.flatMap(event => awardRowsForPlaces({
      group: `Doubles - ${division}`,
      basis: `Planned category // ${event} // 2 awards per team`,
      places: Number(state.awards.doublesPlaces) || 0,
      items: state.awards.doublesItems,
      unitsForPlace: () => 2
    })));
}

function relayAwardRows() {
  const divisions = plannedRelayAwardDivisions();
  const events = plannedEventsForGroup("Timed Relay");
  return divisions.flatMap(division => events.flatMap(event => awardRowsForPlaces({
      group: `Relay Teams - ${division}`,
      basis: `Planned category // ${event} // ${state.awards.relayUnits} awards per team`,
      places: Number(state.awards.relayPlaces) || 0,
      items: state.awards.relayItems,
      unitsForPlace: () => state.awards.relayUnits
    })));
}

function overallAwardRows() {
  return awardOverallGroups.flatMap(group => {
    const config = state.awards.overall[group.key] || {};
    const places = Number(config.limit) || 0;
    if (!places) return [];
    return [{
      group: group.label,
      basis: group.basis,
      place: `Top ${places}`,
      item: normalizeAwardItem(config.item),
      quantity: places
    }];
  });
}

function plannedIndividualAwardDivisions() {
  const settings = state.divisionSettings || defaultDivisionSettings;
  const specialDivisions = divisionRanges(settings.special || [], "Special");
  const configuredSpecialDivisions = state.settings?.separateSpecialDivisionsByGender === true
    ? specialDivisions.flatMap(division => [`${division} M`, `${division} F`])
    : specialDivisions;
  const configured = [
    ...divisionRanges(divisionPath(settings, "male", "Male")).flat(),
    ...divisionRanges(divisionPath(settings, "female", "Female")).flat(),
    ...configuredSpecialDivisions
  ];
  const registered = (state.stackers || [])
    .map(stacker => stacker.customDivision || stacker.division)
    .filter(Boolean);
  return sortedDivisions([...configured, ...registered]);
}

function plannedDoublesAwardDivisions() {
  const settings = state.divisionSettings || defaultDivisionSettings;
  const configured = [
    ...teamDivisionRanges(settings.doubles || []),
    ...teamDivisionRanges(settings.childParentDoubles || [], "Child/Parent "),
    ...teamDivisionRanges(settings.specialDoubles || [], "SS "),
    ...teamDivisionRanges(settings.specialChildParentDoubles || [], "SS Child/Parent ")
  ];
  const registered = (state.doubles || []).map(team => doubleDivision(team)).filter(Boolean);
  return sortedDivisions([...configured, ...registered]);
}

function plannedRelayAwardDivisions() {
  const settings = state.divisionSettings || defaultDivisionSettings;
  const configured = teamDivisionRanges(settings.timedRelay || []);
  const registered = (state.relays || []).map(team => relayDivision(team)).filter(Boolean);
  return sortedDivisions([...configured, ...registered]);
}

function plannedEventsForGroup(group) {
  return (state.events?.[group] || eventGroups[group] || []).filter(Boolean);
}

function awardRowsForPlaces({ group, basis, places, items, unitsForPlace }) {
  return Array.from({ length: places }, (_, index) => ({
    group,
    basis,
    place: ordinal(index + 1),
    item: normalizeAwardItem(items[index]),
    quantity: unitsForPlace(index)
  }));
}

function groupItemsByValue(items, getValue) {
  return items.reduce((acc, item) => {
    const key = getValue(item) || "Open";
    if (!acc[key]) acc[key] = [];
    acc[key].push(item);
    return acc;
  }, {});
}

function doubleAwardUnits(team) {
  if (!team) return 0;
  const registered = registeredDoubleMemberIds(team).length;
  return Math.max(registered || 0, team.parentName ? 2 : 0, 1);
}

function eligibleOverallStackers(key) {
  return state.stackers.filter(stacker => {
    const special = stacker.special === "Yes" || String(stacker.division || "").startsWith("SS ");
    if (key === "male") return !special && stacker.gender === "M";
    if (key === "female") return !special && stacker.gender === "F";
    if (key === "specialMale") return special && stacker.gender === "M";
    if (key === "specialFemale") return special && stacker.gender === "F";
    if (key === "combined") return !special;
    return false;
  });
}

function exportAwardsCsv() {
  saveAwards(false);
  const headers = ["Award Group", "Basis", "Places", "Item", "Quantity"];
  const rows = awardPlanRows().map(row => [row.group, row.basis, row.place, row.item, row.quantity]);
  downloadText("awards-planner.csv", [headers, ...rows].map(csvLine).join("\n"), "text/csv");
}

function ordinal(number) {
  const labels = { 1: "Champion", 2: "1st Runner Up", 3: "2nd Runner Up" };
  return labels[number] || `${number}th`;
}

function updateDoubleFormMode() {
  const status = val("doubleStatus");
  const generatedType = detectedDoubleType();
  setValue("doubleType", generatedType === "child_parent" ? "Child / Parent" : "Normal Doubles");
  document.getElementById("doubleTwo")?.toggleAttribute("disabled", generatedType === "normal" && status === "pending");
  const parentInput = document.getElementById("doubleParentName");
  if (parentInput) {
    parentInput.placeholder = "External parent / guardian name";
  }
  showSelectedDoubleWarnings();
}

function detectedDoubleType() {
  return val("doubleParentName").trim() ? "child_parent" : "normal";
}

function showSelectedDoubleWarnings() {
  const box = document.getElementById("doubleWarning");
  const selected = [selectedStackerId("doubleOne"), selectedStackerId("doubleTwo")].filter(Boolean);
  const conflicts = selected
    .map(id => ({ id, team: doublesForStacker(id).find(existing => existing.id !== editingDoubleId) }))
    .filter(item => item.team);
  if (!box) return;
  if (!conflicts.length) {
    box.hidden = true;
    box.textContent = "";
    return;
  }
  box.hidden = false;
  box.textContent = `${conflicts.map(item => `${stackerName(item.id)} is now in ${item.team.id}`).join("; ")}. Saving will remove them from the current doubles team and pair them here.`;
}

function showDoubleMessage() {
  const box = document.getElementById("doubleMessage");
  if (!box) return;
  if (!doubleFlashMessage) {
    box.hidden = true;
    return;
  }
  box.hidden = false;
  box.textContent = doubleFlashMessage.text;
  box.classList.toggle("error", doubleFlashMessage.type === "error");
  doubleFlashMessage = null;
}

function renderPaperwork() {
  const stackers = [...state.stackers].sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  const options = stackers.map(stacker => `<option value="${esc(stacker.id)}">${esc(stacker.id)} - ${esc(stacker.name)}</option>`).join("");
  const from = document.getElementById("printRangeFrom");
  const to = document.getElementById("printRangeTo");
  if (from) from.innerHTML = options;
  if (to) {
    to.innerHTML = options;
    if (stackers.length) to.value = stackers[stackers.length - 1].id;
  }
  const participantAvailability = {
    individuals: state.stackers.length > 0,
    doubles: printableDoublesTeams().length > 0,
    relay: completedRelays().length > 0
  };
  document.querySelectorAll("[data-participant-group]").forEach(button => {
    button.toggleAttribute("hidden", !participantAvailability[button.dataset.participantGroup]);
  });
  document.getElementById("paperOutput").innerHTML = `<h2>Preview</h2><p class="muted">Choose a print item to generate a printable preview.</p>`;
}

function renderCompetition() {
  populateEntryTypeOptions();
  populateParticipants();
  populateFinalSheetSelect();
  document.getElementById("entryType")?.addEventListener("change", updateCompetitionEntryMode);
  document.getElementById("resultStage").addEventListener("change", updateCompetitionEntryMode);
  document.getElementById("finalSheetSelect")?.addEventListener("change", event => loadFinalSheet(event.target.value));
  document.getElementById("finalSheetId")?.addEventListener("keydown", event => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    loadFinalSheetFromInput();
  });
  document.getElementById("timeSheetId").addEventListener("input", event => {
    const normalized = normalizePrelimEntryId(event.target.value);
    if (!activePrelimParticipantId || normalized === activePrelimParticipantId) return;
    activePrelimParticipantId = "";
    activePrelimParticipantType = "";
    hidePrelimEntryFields();
  });
  document.getElementById("timeSheetId").addEventListener("keydown", event => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    loadPrelimParticipant();
  });
  document.querySelectorAll(".prelim-time-input").forEach(input => {
    input.addEventListener("blur", () => normalizeCompetitionTimeInput(input));
    input.addEventListener("keydown", event => {
      if (event.key !== "Enter") return;
      event.preventDefault();
      if (!input.value.trim()) input.value = "999";
      if (!normalizeCompetitionTimeInput(input)) return;
      const visibleInputs = visiblePrelimTimeInputs();
      const index = visibleInputs.indexOf(input);
      if (visibleInputs[index + 1]) {
        visibleInputs[index + 1].focus();
        visibleInputs[index + 1].select();
      } else {
        void savePrelimResults({ blankAsScratch: true });
      }
    });
  });
  document.querySelectorAll(".final-time-input").forEach(input => {
    input.addEventListener("blur", () => {
      if (normalizeFinalTimeInput(input)) updateFinalSheetComputedColumns();
    });
    input.addEventListener("keydown", event => {
      if (event.key !== "Enter") return;
      event.preventDefault();
      if (!input.value.trim()) input.value = "999";
      if (!normalizeFinalTimeInput(input)) return;
      updateFinalSheetComputedColumns();
      const inputs = visibleFinalTimeInputs();
      const index = inputs.indexOf(input);
      if (inputs[index + 1]) {
        inputs[index + 1].focus();
        inputs[index + 1].select();
      } else {
        saveFinalResults();
      }
    });
  });
  updateCompetitionEntryMode();
  drawResultRows();
  drawMissingTimes();
}

function populateEntryTypeOptions() {
  const select = document.getElementById("entryType");
  if (!select) return;
  const current = select.value;
  const options = availableEntryTypes();
  select.innerHTML = options.map(type => `<option>${esc(type)}</option>`).join("");
  select.value = options.includes(current) ? current : options[0] || "Individual";
}

function availableEntryTypes() {
  return [
    eventGroupEnabled("Individuals") ? "Individual" : "",
    eventGroupEnabled("Doubles") ? "Doubles" : "",
    eventGroupEnabled("Timed Relay") ? "Relay" : ""
  ].filter(Boolean);
}

function updateCompetitionEntryMode() {
  const stage = val("resultStage") || "Prelims";
  const type = val("entryType") || "Individual";
  const isPrelim = stage === "Prelims";
  const isFinals = stage === "Finals";
  const prelimPanel = document.getElementById("prelimEntryPanel");
  const advancedPanel = document.getElementById("advancedEntryPanel");
  const typeField = document.getElementById("entryTypeField");
  const prelimMissingPanel = document.getElementById("prelimMissingPanel");
  if (prelimPanel) prelimPanel.hidden = !isPrelim;
  if (advancedPanel) advancedPanel.hidden = isPrelim;
  if (typeField) typeField.hidden = true;
  if (prelimMissingPanel) prelimMissingPanel.hidden = !isPrelim;
  const heading = document.getElementById("entryHeading");
  if (heading) heading.textContent = isPrelim ? "Prelim Entry" : isFinals ? "Finals Entry" : `${type} ${stage} Entry`;
  if (isFinals) {
    populateFinalSheetSelect();
    if (activeFinalSheetId) loadFinalSheet(activeFinalSheetId, false);
  } else if (!isPrelim) {
    populateParticipants();
  }
  drawResultRows();
}

function loadPrelimParticipant() {
  const participant = resolvePrelimParticipant(val("timeSheetId"));
  if (!participant) {
    activePrelimParticipantId = "";
    activePrelimParticipantType = "";
    hidePrelimEntryFields();
    showPrelimMessage("Participant ID not found. Check the printed sheet and try again.", true);
    document.getElementById("timeSheetId")?.focus();
    return null;
  }
  activePrelimParticipantId = participant.id;
  activePrelimParticipantType = participant.type;
  setValue("timeSheetId", participant.id);
  setValue("entryType", participant.entryType);
  const summary = document.getElementById("prelimStackerSummary");
  const identity = prelimParticipantIdentity(participant);
  summary.hidden = false;
  summary.innerHTML = `<div><span>Participant ID</span><strong>${esc(participant.id)}</strong></div><div><span>${esc(identity.label)}</span><strong>${esc(identity.value)}</strong></div><div><span>Division</span><strong>${esc(participant.division || "Open")}</strong></div><div><span>Organization / Location</span><strong>${esc(participant.org || "Independent")} // ${esc(participant.country || "--")}</strong></div>`;
  Object.entries(prelimEventFieldIds).forEach(([event, fieldId]) => {
    const result = findPrelimEntryResult(participant, event);
    setValue(fieldId, participant.events.includes(event) ? prelimResultInputValue(result) : "");
  });
  showPrelimEntryFields(participant.events);
  const hasExistingResult = participant.events.some(event => Boolean(findPrelimEntryResult(participant, event)));
  showPrelimMessage(`${hasExistingResult ? "Editing Existing Result" : "Ready for Entry"}: ${participant.id} ${participant.name}.`, false);
  const firstInput = visiblePrelimTimeInputs()[0];
  firstInput?.focus();
  firstInput?.select();
  return participant;
}

function normalizePrelimEntryId(rawId) {
  const input = String(rawId || "").trim();
  const dotted = /^([123])\.(\d+)$/.exec(input);
  if (dotted) return `${dotted[1]}.${Number(dotted[2])}`;
  const compact = /^([123])(\d+)$/.exec(input);
  if (compact) return `${compact[1]}.${Number(compact[2])}`;
  return "";
}

function prelimParticipantIdentity(participant) {
  if (participant.type === "Doubles") {
    return { label: "Doubles Partners", value: participantName("Doubles", participant.id) };
  }
  if (participant.type === "Relay" || participant.type === "Timed Relay") {
    const members = relayMemberIds(participant).map(stackerName).filter(name => name !== "Unknown");
    return {
      label: "Relay Team / Members",
      value: [participantName("Timed Relay", participant.id), members.join(" / ")].filter(Boolean).join(" — ")
    };
  }
  return { label: "Individual", value: participant.name };
}

function resolvePrelimParticipant(rawId) {
  const id = normalizePrelimEntryId(rawId);
  if (!id) return null;
  setValue("timeSheetId", id);
  const prefix = id.split(".")[0];
  const config = prelimEntryConfig[prefix];
  if (!config) return null;
  if (prefix === "1") {
    const stacker = state.stackers.find(item => item.id === id);
    return stacker ? { ...config, ...stacker, events: prelimEventsForParticipant(config), name: stacker.name } : null;
  }
  if (prefix === "2") {
    const team = findDoublesTeam(id);
    if (!team) return null;
    const members = registeredDoubleMemberIds(team).map(memberId => state.stackers.find(item => item.id === memberId) || {});
    return {
      ...team,
      ...config,
      id: team.id,
      events: prelimEventsForParticipant(config),
      name: participantName("Doubles", id),
      org: team.org || members.find(member => member.org)?.org || "",
      country: teamCountry(team)
    };
  }
  const relay = state.relays.find(item => item.id === id && relayIsComplete(item));
  return relay ? { ...relay, ...config, id: relay.id, events: prelimEventsForParticipant(config), name: participantName("Timed Relay", id), division: relayDivision(relay), org: relayLocation(relay), country: relay.country || relayCountryForMembers(relayMemberIds(relay)) } : null;
}

function typeEventGroup(type) {
  if (type === "Doubles") return "Doubles";
  if (type === "Relay" || type === "Timed Relay") return "Timed Relay";
  return "Individuals";
}

function prelimEventsForParticipant(config) {
  const configured = state.events?.[typeEventGroup(config.entryType)];
  return Array.isArray(configured) && configured.length ? configured : config.events;
}

function showPrelimEntryFields(events) {
  const grid = document.getElementById("prelimEventGrid");
  const actions = document.getElementById("prelimSaveActions");
  if (grid) grid.hidden = false;
  if (actions) actions.hidden = false;
  document.querySelectorAll("[data-prelim-event]").forEach(label => {
    label.hidden = !events.includes(label.dataset.prelimEvent);
  });
}

function hidePrelimEntryFields() {
  const summary = document.getElementById("prelimStackerSummary");
  const grid = document.getElementById("prelimEventGrid");
  const actions = document.getElementById("prelimSaveActions");
  if (summary) summary.hidden = true;
  if (grid) grid.hidden = true;
  if (actions) actions.hidden = true;
  Object.values(prelimEventFieldIds).forEach(id => setValue(id, ""));
}

function visiblePrelimTimeInputs() {
  return [...document.querySelectorAll("[data-prelim-event]")]
    .filter(label => !label.hidden)
    .map(label => label.querySelector("input"))
    .filter(Boolean);
}

function prelimResultInputValue(result) {
  if (!result) return "";
  const summary = calculateBestResult(result);
  if (summary.status === "scratch") return "999";
  return Number.isFinite(summary.bestTime) ? summary.bestTime.toFixed(3) : "999";
}

function clearPrelimEntry() {
  activePrelimParticipantId = "";
  activePrelimParticipantType = "";
  ["timeSheetId", "prelim333", "prelim363", "prelimCycle"].forEach(id => setValue(id, ""));
  hidePrelimEntryFields();
  const message = document.getElementById("prelimEntryMessage");
  if (message) message.hidden = true;
  document.getElementById("timeSheetId")?.focus();
}

function showPrelimMessage(text, isError = false) {
  const message = document.getElementById("prelimEntryMessage");
  if (!message) return;
  message.hidden = false;
  message.textContent = text;
  message.classList.toggle("error", isError);
}

function parseCompetitionTime(rawValue) {
  const raw = String(rawValue ?? "").trim();
  if (!raw) return { kind: "blank", value: null };
  if (raw === "999") return { kind: "scratch", value: 999 };
  let seconds;
  const minuteMatch = /^(\d+):([0-5]?\d)(?:\.(\d{1,3}))?$/.exec(raw);
  if (minuteMatch) {
    const fraction = (minuteMatch[3] || "").padEnd(3, "0");
    seconds = Number(minuteMatch[1]) * 60 + Number(minuteMatch[2]) + Number(`0.${fraction || "0"}`);
  } else if (/^\d+$/.test(raw)) {
    seconds = Number(raw) / 1000;
  } else if (/^\d+\.\d{1,3}$/.test(raw)) {
    seconds = Number(raw);
  } else {
    return { kind: "invalid", value: null };
  }
  if (!Number.isFinite(seconds) || seconds <= 0 || seconds > 600) return { kind: "invalid", value: null };
  return { kind: "time", value: Math.round(seconds * 1000) / 1000 };
}

function normalizeCompetitionTimeInput(input) {
  const parsed = parseCompetitionTime(input.value);
  if (parsed.kind === "blank") return true;
  if (parsed.kind === "invalid") {
    showPrelimMessage(`Invalid time: ${input.value}. Enter a time to 3 decimals or 999 for scratch.`, true);
    input.focus();
    input.select();
    return false;
  }
  input.value = parsed.kind === "scratch" ? "999" : parsed.value.toFixed(3);
  return true;
}

function savePrelimResults(options = {}) {
  if (prelimSaveInFlight) return prelimSaveInFlight;
  const pipeline = persistPrelimResults(options).finally(() => {
    prelimSaveInFlight = null;
  });
  prelimSaveInFlight = pipeline;
  return pipeline;
}

async function persistPrelimResults(options = {}) {
  const focusedFieldId = document.activeElement?.id || "";
  const participant = resolvePrelimParticipant(activePrelimParticipantId || val("timeSheetId"));
  if (!participant || (activePrelimParticipantType && participant.type !== activePrelimParticipantType)) {
    showPrelimMessage("Find a valid participant before saving times.", true);
    document.getElementById("timeSheetId")?.focus();
    return;
  }
  activePrelimParticipantId = participant.id;
  activePrelimParticipantType = participant.type;
  const entries = participant.events.map(event => {
    const fieldId = prelimEventFieldIds[event];
    if (options.blankAsScratch && !val(fieldId).trim()) setValue(fieldId, "999");
    return { event, fieldId, parsed: parseCompetitionTime(val(fieldId)) };
  });
  const invalid = entries.find(entry => entry.parsed.kind === "invalid");
  if (invalid) {
    showPrelimMessage(`Invalid ${invalid.event} time. Enter a time to 3 decimals or 999 for scratch.`, true);
    document.getElementById(invalid.fieldId)?.focus();
    document.getElementById(invalid.fieldId)?.select();
    return;
  }
  const completed = entries.filter(entry => entry.parsed.kind !== "blank");
  if (!completed.length) {
    showPrelimMessage("Enter at least one event time before saving.", true);
    document.getElementById("prelim333")?.focus();
    return;
  }
  const previousState = structuredClone(state);
  const resultStage = prelimEntryResultStage();
  let created = 0;
  let updated = 0;
  const changes = [];
  completed.forEach(entry => {
    const existing = findPrelimEntryResult(participant, entry.event);
    const result = {
      id: existing?.id || crypto.randomUUID(),
      stage: resultStage,
      type: participant.type,
      participant: participant.id,
      event: entry.event,
      attempts: [entry.parsed.value],
      penalty: entry.parsed.kind === "scratch" ? 999 : 0
    };
    changes.push({ event: entry.event, before: existing || null, after: result });
    if (existing) {
      state.results = state.results.filter(item => !(prelimEntryLookupStages().includes(item.stage) && item.type === participant.type && item.participant === participant.id && normalizeEventName(item.event) === normalizeEventName(entry.event)));
      state.results.push(result);
      updated += 1;
    } else {
      state.results.push(result);
      created += 1;
    }
    setValue(entry.fieldId, entry.parsed.kind === "scratch" ? "999" : entry.parsed.value.toFixed(3));
  });
  appendCompetitionAuditLog({
    action: "results.prelim_saved",
    entityType: "Result",
    entityId: participant.id,
    summary: `${participant.id} ${participant.name}: ${created} added, ${updated} updated for ${resultStage}.`,
    before: changes.map(item => ({ event: item.event, result: item.before })),
    after: changes.map(item => ({ event: item.event, result: item.after }))
  });
  try {
    await saveState();
    const authoritativeState = await repository.load();
    if (!prelimResultsPersisted(authoritativeState, participant, completed)) throw new Error("Saved values could not be verified from the authoritative store.");
  } catch (error) {
    state = previousState;
    showPrelimMessage(`Save failed. Times remain on screen and were not cleared: ${error.message || "unable to verify persistence"}`, true);
    if (focusedFieldId) document.getElementById(focusedFieldId)?.focus();
    return false;
  }
  drawResultRows();
  drawMissingTimes();
  const actionText = [created ? `${created} added` : "", updated ? `${updated} updated` : ""].filter(Boolean).join(", ");
  clearPrelimEntry();
  showPrelimMessage(`${participant.id} ${participant.name}: ${actionText}.`, false);
  return true;
}

function prelimResultsPersisted(authoritativeState, participant, entries) {
  const results = authoritativeState?.results || [];
  return entries.every(entry => {
    const result = results.find(item => item.stage === prelimEntryResultStage() && item.type === participant.type && item.participant === participant.id && normalizeEventName(item.event) === normalizeEventName(entry.event));
    if (!result) return false;
    const expectedPenalty = entry.parsed.kind === "scratch" ? 999 : 0;
    return Number(result.penalty || 0) === expectedPenalty && Number(result.attempts?.[0]) === Number(entry.parsed.value);
  });
}

function prelimEntryResultStage() {
  return state.settings?.prelims === "0" && state.settings?.finals === "1" ? "Finals" : "Prelims";
}

function prelimEntryLookupStages() {
  const stage = prelimEntryResultStage();
  return stage === "Finals" ? ["Finals", "Prelims"] : ["Prelims"];
}

function findPrelimEntryResult(participant, event, results = state.results) {
  return results.find(item => prelimEntryLookupStages().includes(item.stage) && item.type === participant.type && item.participant === participant.id && normalizeEventName(item.event) === normalizeEventName(event));
}

function populateFinalSheetSelect() {
  const select = document.getElementById("finalSheetSelect");
  if (!select) return;
  const current = activeFinalSheetId || select.value;
  const sheets = finalSheets();
  select.innerHTML = [`<option value="">please choose</option>`].concat(sheets.map(sheet => (
    `<option value="${esc(sheet.id)}">${esc(sheet.division)} // ${esc(sheet.event)} // ID:${esc(sheet.id)}</option>`
  ))).join("");
  if (current && sheets.some(sheet => sheet.id === current)) select.value = current;
  drawFinalMissingSummary(sheets);
}

function finalSheets() {
  const sheetGroups = new Map();
  state.results
    .filter(result => result.stage === "Prelims" && Number.isFinite(official(result)) && official(result) < 999)
    .forEach(result => {
      const meta = competitionParticipantMeta(result.type, result.participant);
      const normalizedEvent = normalizeEventName(result.event);
      const event = normalizedEvent === "cycle" ? "Cycle" : result.event;
      const division = meta.division || "Open";
      const typeKey = result.type === "Doubles" ? "2" : result.type === "Timed Relay" ? "3" : "1";
      if (!eventGroupEnabled(typeEventGroup(result.type))) return;
      const key = `${typeKey}|${division}|${normalizeEventName(event)}`;
      if (!sheetGroups.has(key)) {
        sheetGroups.set(key, { typeKey, type: result.type, entryType: finalEntryType(result.type), division, event, prelimRows: [] });
      }
      sheetGroups.get(key).prelimRows.push({
        participant: result.participant,
        name: meta.name,
        org: meta.org,
        country: meta.country,
        region: meta.region,
        gender: meta.gender,
        division,
        event,
        prelimTime: official(result),
        result
      });
    });

  const counters = { "1": 0, "2": 0, "3": 0 };
  return [...sheetGroups.values()]
    .sort(compareFinalSheetGroups)
    .map(group => {
      counters[group.typeKey] += 1;
      const id = `${group.typeKey}.${counters[group.typeKey]}`;
      const finalists = finalSheetQualifiers(group)
        .map((row, index) => ({ ...row, qualifierRank: index + 1 }))
        .reverse();
      return { ...group, id, finalists };
    })
    .filter(sheet => sheet.finalists.length);
}

function compareFinalSheetGroups(a, b) {
  return Number(a.typeKey) - Number(b.typeKey)
    || compareDivisionNames(a.division, b.division)
    || finalEventSort(a.event) - finalEventSort(b.event)
    || String(a.event).localeCompare(String(b.event), undefined, { numeric: true, sensitivity: "base" });
}

function finalEventSort(event) {
  const normalized = normalizeEventName(event);
  return { "3-3-3": 1, "3-6-3": 2, cycle: 3 }[normalized] || 9;
}

function finalSheetQualifiers(group) {
  const limit = finalAdvanceLimit(group.type, group.division);
  const sorted = [...group.prelimRows]
    .sort((a, b) => a.prelimTime - b.prelimTime || String(a.name).localeCompare(String(b.name), undefined, { sensitivity: "base" }));
  return sorted.slice(0, Math.min(limit || sorted.length, sorted.length));
}

function finalAdvanceLimit(type, division = "") {
  if (type === "Doubles") {
    if (/child\/parent/i.test(division)) return Number(state.settings.advanceCpDoubles) || Number(state.settings.advanceDoubles) || 0;
    return Number(state.settings.advanceDoubles) || 0;
  }
  if (type === "Timed Relay") return Number(state.settings.advanceRelay) || 0;
  return Number(state.settings.advanceIndividuals) || 0;
}

function finalEntryType(type) {
  if (type === "Doubles") return "Doubles";
  if (type === "Timed Relay") return "Relay";
  return "Individuals";
}

function drawFinalMissingSummary(sheets = finalSheets()) {
  const groups = ["1", "2", "3"].map(typeKey => {
    const typeSheets = sheets.filter(sheet => sheet.typeKey === typeKey);
    const complete = typeSheets.filter(sheet => finalSheetIsComplete(sheet)).length;
    const label = typeKey === "1" ? "Individuals" : typeKey === "2" ? "Doubles" : "Relay";
    const missing = typeSheets.filter(sheet => !finalSheetIsComplete(sheet));
    return { label, complete, total: typeSheets.length, missing };
  });
  const box = document.getElementById("finalMissingSummary");
  if (!box) return;
  box.innerHTML = groups.map(group => `
    <section class="missing-group">
      <h3>${esc(group.label)} ${group.complete} complete, ${group.total - group.complete} remaining of ${group.total} total</h3>
      <div class="missing-entry-list">${group.missing.map(sheet => `
        <button class="missing-entry-button" data-action="load-final-missing" data-id="${esc(sheet.id)}" type="button">
          <span><strong>${esc(sheet.id)}</strong>${esc(sheet.division)} // ${esc(sheet.event)}</span>
          <small>${esc(sheet.type)}</small>
        </button>`).join("") || `<p class="muted">No missing divisions</p>`}</div>
    </section>
  `).join("");
}

function finalSheetIsComplete(sheet) {
  return sheet.finalists.every(finalist => {
    const result = finalResultFor(sheet, finalist.participant);
    return result && finalResultAttempts(result).length;
  });
}

function loadFinalSheetFromInput() {
  const id = normalizePrelimEntryId(val("finalSheetId"));
  loadFinalSheet(id);
}

function loadFinalSheet(id, focusFirst = true) {
  const sheet = finalSheets().find(item => item.id === id);
  if (!sheet) {
    clearFinalSheet(false);
    showFinalMessage("Final sheet ID not found. Check the printed finals sheet and try again.", true);
    document.getElementById("finalSheetId")?.focus();
    return null;
  }
  activeFinalSheetId = sheet.id;
  setValue("finalSheetId", sheet.id);
  setValue("finalSheetSelect", sheet.id);
  const summary = document.getElementById("finalSheetSummary");
  if (summary) {
    summary.hidden = false;
    summary.innerHTML = `<div><span>Final Sheet ID</span><strong>${esc(sheet.id)}</strong></div><div><span>${esc(sheet.entryType)}</span><strong>${esc(sheet.division)} // ${esc(sheet.event)}</strong></div><div><span>Advance</span><strong>Top ${Math.min(finalAdvanceLimit(sheet.type, sheet.division) || sheet.finalists.length, sheet.finalists.length)} of ${sheet.prelimRows.length}</strong></div><div><span>Order</span><strong>Slowest qualifier competes first</strong></div>`;
  }
  drawFinalSheetRows(sheet);
  showFinalMessage(`Ready for Finals ${sheet.id}: ${sheet.entryType} // ${sheet.division} // ${sheet.event}.`, false);
  if (focusFirst) {
    const firstInput = visibleFinalTimeInputs()[0];
    firstInput?.focus();
    firstInput?.select();
  }
  return sheet;
}

function drawFinalSheetRows(sheet) {
  const wrap = document.getElementById("finalSheetTableWrap");
  const actions = document.getElementById("finalSaveActions");
  const rows = document.getElementById("finalSheetRows");
  if (!wrap || !actions || !rows) return;
  wrap.hidden = false;
  actions.hidden = false;
  const placements = finalPlacements(sheet);
  rows.innerHTML = sheet.finalists.map(finalist => {
    const result = finalResultFor(sheet, finalist.participant);
    const attempts = finalResultInputValues(result);
    const placement = placements.get(finalist.participant);
    return `<tr data-final-participant="${esc(finalist.participant)}">
      <td><strong>${finalist.qualifierRank}</strong></td>
      <td><strong>${esc(finalist.name)}</strong><br><small>${esc(finalParticipantSubline(sheet.type, finalist.participant))}</small></td>
      <td>${fmt(finalist.prelimTime)}</td>
      ${[0, 1, 2].map(index => `<td><input class="final-time-input" data-final-participant="${esc(finalist.participant)}" data-final-attempt="${index + 1}" type="text" inputmode="decimal" value="${esc(attempts[index] || "")}" placeholder="3.145 / 999" /></td>`).join("")}
      <td data-final-best="${esc(finalist.participant)}">${esc(finalBestDisplay(result))}</td>
      <td data-final-place="${esc(finalist.participant)}">${placement || ""}</td>
    </tr>`;
  }).join("");
  document.querySelectorAll(".final-time-input").forEach(input => {
    input.addEventListener("blur", () => {
      if (normalizeFinalTimeInput(input)) updateFinalSheetComputedColumns();
    });
    input.addEventListener("keydown", event => {
      if (event.key !== "Enter") return;
      event.preventDefault();
      if (!input.value.trim()) input.value = "999";
      if (!normalizeFinalTimeInput(input)) return;
      updateFinalSheetComputedColumns();
      const inputs = visibleFinalTimeInputs();
      const index = inputs.indexOf(input);
      if (inputs[index + 1]) {
        inputs[index + 1].focus();
        inputs[index + 1].select();
      } else {
        saveFinalResults();
      }
    });
  });
}

function finalParticipantSubline(type, participantId) {
  if (type === "Individual") {
    const stacker = state.stackers.find(item => item.id === participantId) || {};
    return `ID: ${participantId} // Age: ${stacker.age || ageOnCompetitionDate(stacker.dob, state.settings.start) || "--"} // ${stacker.country || "--"}`;
  }
  return `ID: ${participantId} // ${competitionParticipantMeta(type, participantId).country || "--"}`;
}

function finalResultFor(sheet, participantId) {
  return state.results.find(result => result.stage === "Finals" && result.type === sheet.type && result.participant === participantId && normalizeEventName(result.event) === normalizeEventName(sheet.event));
}

function finalResultInputValues(result) {
  if (!result) return ["", "", ""];
  return [0, 1, 2].map(index => {
    const value = result.attempts?.[index];
    if (!Number.isFinite(value)) return "";
    return value >= 999 ? "999" : value.toFixed(3);
  });
}

function finalResultAttempts(result) {
  return (result?.attempts || []).filter(value => Number.isFinite(value));
}

function finalBestDisplay(result) {
  const key = finalTieBreakKey(result);
  return Number.isFinite(key[0]) ? fmt(key[0]) : "";
}

function normalizeFinalTimeInput(input) {
  const parsed = parseCompetitionTime(input.value);
  if (parsed.kind === "blank") return true;
  if (parsed.kind === "invalid") {
    showFinalMessage(`Invalid time: ${input.value}. Enter a time to 3 decimals or 999 for scratch.`, true);
    input.focus();
    input.select();
    return false;
  }
  input.value = parsed.kind === "scratch" ? "999" : parsed.value.toFixed(3);
  return true;
}

function visibleFinalTimeInputs() {
  return [...document.querySelectorAll(".final-time-input")].filter(input => !input.closest("[hidden]"));
}

function updateFinalSheetComputedColumns() {
  const sheet = finalSheets().find(item => item.id === activeFinalSheetId);
  if (!sheet) return;
  const draftResults = finalDraftResults(sheet);
  const placements = finalPlacements(sheet, draftResults);
  sheet.finalists.forEach(finalist => {
    const result = draftResults.find(item => item.participant === finalist.participant);
    const bestCell = document.querySelector(`[data-final-best="${cssEscape(finalist.participant)}"]`);
    const placeCell = document.querySelector(`[data-final-place="${cssEscape(finalist.participant)}"]`);
    if (bestCell) bestCell.textContent = finalBestDisplay(result);
    if (placeCell) placeCell.textContent = placements.get(finalist.participant) || "";
  });
}

function finalDraftResults(sheet) {
  return sheet.finalists.map(finalist => {
    const attempts = [1, 2, 3].map(index => {
      const input = document.querySelector(`.final-time-input[data-final-participant="${cssEscape(finalist.participant)}"][data-final-attempt="${index}"]`);
      const parsed = parseCompetitionTime(input?.value || "");
      if (parsed.kind === "blank" || parsed.kind === "invalid") return null;
      return parsed.kind === "scratch" ? 999 : parsed.value;
    }).filter(value => value !== null);
    return { id: `draft-${finalist.participant}`, stage: "Finals", type: sheet.type, participant: finalist.participant, event: sheet.event, attempts, penalty: 0 };
  });
}

function finalPlacements(sheet, resultRows = null) {
  const rows = (resultRows || sheet.finalists.map(finalist => finalResultFor(sheet, finalist.participant)).filter(Boolean))
    .filter(result => FinalsReportEngine.classifyResult(result).eligibleForRanking)
    .map(result => ({ result, participant: result.participant, name: participantName(result.type || sheet.type, result.participant), tieKey: FinalsReportEngine.finalTieKey(result) }));
  return new Map(FinalsReportEngine.rankFinalRows(rows).map(row => [row.participant, row.rank]));
}

function compareFinalResults(a, b) {
  const left = finalTieBreakKey(a);
  const right = finalTieBreakKey(b);
  return FinalsReportEngine.compareKeys(left, right);
}

function finalTieBreakKey(result) {
  return FinalsReportEngine.finalTieKey(result);
}

function saveFinalResults() {
  const sheet = finalSheets().find(item => item.id === activeFinalSheetId);
  if (!sheet) {
    showFinalMessage("Find a valid final sheet before saving.", true);
    return;
  }
  const invalid = visibleFinalTimeInputs().find(input => parseCompetitionTime(input.value).kind === "invalid");
  if (invalid) {
    showFinalMessage(`Invalid time: ${invalid.value}. Enter a time to 3 decimals or 999 for scratch.`, true);
    invalid.focus();
    invalid.select();
    return;
  }
  const draftRows = finalDraftResults(sheet);
  let saved = 0;
  const changes = [];
  draftRows.forEach(row => {
    const before = state.results.find(result => result.stage === "Finals" && result.type === row.type && result.participant === row.participant && normalizeEventName(result.event) === normalizeEventName(row.event)) || null;
    state.results = state.results.filter(result => !(result.stage === "Finals" && result.type === row.type && result.participant === row.participant && normalizeEventName(result.event) === normalizeEventName(row.event)));
    if (!row.attempts.length) return;
    const after = { id: crypto.randomUUID(), stage: "Finals", type: row.type, participant: row.participant, event: row.event, attempts: row.attempts, penalty: 0 };
    state.results.push(after);
    changes.push({ participant: row.participant, before, after });
    saved += 1;
  });
  appendCompetitionAuditLog({
    action: "results.final_saved",
    entityType: "Result",
    entityId: sheet.id,
    summary: `${sheet.id}: ${saved} final result${saved === 1 ? "" : "s"} recorded.`,
    before: changes.map(item => ({ participant: item.participant, result: item.before })),
    after: changes.map(item => ({ participant: item.participant, result: item.after }))
  });
  showFinalMessage(`${sheet.id} saved. ${saved} final result${saved === 1 ? "" : "s"} recorded.`, false);
  populateFinalSheetSelect();
  clearFinalSheet();
  showFinalMessage(`${sheet.id} saved. ${saved} final result${saved === 1 ? "" : "s"} recorded.`, false);
  const sheetInput = document.getElementById("finalSheetId");
  sheetInput?.focus();
  sheetInput?.select();
  drawResultRows();
}

function clearFinalSheet(clearInput = true) {
  activeFinalSheetId = "";
  if (clearInput) {
    setValue("finalSheetId", "");
    setValue("finalSheetSelect", "");
  }
  ["finalSheetSummary", "finalSheetTableWrap", "finalSaveActions"].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.hidden = true;
  });
  const rows = document.getElementById("finalSheetRows");
  if (rows) rows.innerHTML = "";
  const message = document.getElementById("finalEntryMessage");
  if (message) message.hidden = true;
}

function showFinalMessage(text, isError = false) {
  const message = document.getElementById("finalEntryMessage");
  if (!message) return;
  message.hidden = false;
  message.textContent = text;
  message.classList.toggle("error", isError);
}

function renderReports() {
  renderReportTabs();
  populateFinalsReportFilters();
  runFinalsReport();
  populateReportBuilder();
  runReport();
}

function renderReportTabs() {
  document.querySelectorAll("[data-report-tab]").forEach(button => {
    button.classList.toggle("active", button.dataset.reportTab === reportTab);
  });
  document.querySelectorAll("[data-report-section]").forEach(section => {
    section.hidden = section.dataset.reportSection !== reportTab;
  });
}

function switchReportTab(tabName) {
  reportTab = ["finals", "admin"].includes(tabName) ? tabName : "finals";
  renderReportTabs();
}

function populateFinalsReportFilters() {
  const division = document.getElementById("finalsDivision");
  if (division) {
    const current = division.value || "all";
    const values = sortedDivisions([...state.divisions, ...state.stackers.map(stacker => stacker.division)]);
    division.innerHTML = [`<option value="all">All divisions</option>`, ...values.map(value => `<option value="${esc(value)}">${esc(value)}</option>`)].join("");
    division.value = [...division.options].some(option => option.value === current) ? current : "all";
  }
  const event = document.getElementById("finalsEvent");
  if (event) {
    const current = event.value || "all";
    const events = [...new Set(state.results.map(result => result.event).filter(Boolean))]
      .sort((left, right) => String(left).localeCompare(String(right), undefined, { numeric: true }));
    event.innerHTML = [`<option value="all">All events</option>`, ...events.map(value => `<option value="${esc(value)}">${esc(value)}</option>`), `<option value="All-Around">All-Around</option>`].join("");
    event.value = [...event.options].some(option => option.value === current) ? current : "all";
  }
  populateReportSelect("finalsOrg", uniqueReportValues(state.stackers, "org"));
  populateReportSelect("finalsCountry", uniqueReportValues(state.stackers, "country"));
  populateReportSelect("finalsRegion", uniqueReportValues(state.stackers, "region"));
}

function finalsReportFilters() {
  return { participantType: val("finalsParticipantType") || "all", division: val("finalsDivision") || "all", event: val("finalsEvent") || "all", category: val("finalsCategory") || "mixed", gender: val("finalsGender"), org: val("finalsOrg"), country: val("finalsCountry"), region: val("finalsRegion") };
}

function finalMissingReportRows(filters) {
  return finalSheets().flatMap(sheet => {
    const placements = finalPlacements(sheet);
    return sheet.finalists.map(finalist => {
      const result = finalResultFor(sheet, finalist.participant);
      const classification = FinalsReportEngine.classifyResult(result);
      const meta = FinalsReportEngine.participantMeta(state, sheet.type, finalist.participant);
      const status = classification.status === "valid" && (result?.attempts || []).length < 3 ? "incomplete" : classification.status;
      return { ...meta, result, event: sheet.event, attempts: result?.attempts || [], bestValidTime: classification.bestValidTime, resultStatus: status, rank: placements.get(finalist.participant) || null, tie: false };
    });
  }).filter(row => FinalsReportEngine.appliesFilters(row, filters)).filter(row => row.resultStatus !== "valid" || !Number.isFinite(row.rank));
}

function finalsReportDefinition() {
  const kind = val("finalsReportKind") || "final-results";
  const filters = finalsReportFilters();
  const stage = val("finalsReportStage") || "Finals";
  if (stage !== "Finals" || ["final-results", "overall", "division", "event", "missing", "scratch"].includes(kind)) return stageReportDefinition(stage, kind, filters);
  const limit = Number(val("finalsLimit")) || 0;
  let title = "", headers = [], rows = [], contributors = null;
  if (kind === "qualification") {
    const snapshots = state.finalQualificationSnapshots.filter(snapshot => snapshot.status === "Approved");
    title = "Finals Qualification Report";
    headers = ["Status", "Participant Type", "Division", "Event", "Participant", "Prelim Rank", "Prelim Best", "Final Seed", "Final Sheet / Heat", "Tie / Exception"];
    rows = snapshots.flatMap(snapshot => snapshot.selectedQualifiers.map(item => {
      const meta = FinalsReportEngine.participantMeta(state, snapshot.participantType, item.participantId);
      const source = snapshot.sourcePreliminaryResults.find(row => row.participantId === item.participantId) || {};
      return [snapshot.status, snapshot.participantType, snapshot.division, snapshot.event, meta.name, item.preliminaryRank, fmt(source.bestValidTime), item.finalSeed, [item.finalSheetId, item.heat].filter(Boolean).join(" / "), snapshot.tieException?.rationale || "--"];
    }));
    return { kind, title, headers, rows, empty: snapshots.length ? "No qualifiers match the approved snapshots." : "Qualification has not been approved.", filters };
  }
  if (kind === "all-around") {
    return stageAllAroundReportDefinition("Finals", "Finals", filters);
  } else if (kind === "organization") {
    title = "Organization Championship Ranking";
    headers = ["Rank", "Organization", "Champions / 1st", "2nd Places", "3rd Places", "4th Places", "5th Places", "Total Placements", "Participating Stackers", "Individual Entries", "Doubles Teams", "Relay Teams"];
    contributors = FinalsReportEngine.organizationCredits(state, filters);
    rows = contributors.map(row => [row.rank, row.organization, row.counts[1] || 0, row.counts[2] || 0, row.counts[3] || 0, row.counts[4] || 0, row.counts[5] || 0, row.totalPlacements, row.participatingStackers, row.individualEntries, row.doublesTeams, row.relayTeams]);
  } else {
    let reportRows = kind === "top-performance" && String(filters.event).toLowerCase() === "all-around" ? FinalsReportEngine.allAroundRows(state, filters) : FinalsReportEngine.placementRows(state, filters);
    if (kind === "missing") reportRows = finalMissingReportRows(filters);
    if (kind === "top-performance") reportRows = FinalsReportEngine.rankFinalRows(reportRows.filter(row => row.resultStatus === "valid"));
    if (limit) reportRows = reportRows.slice(0, limit);
    title = kind === "top-performance" ? `${filters.category === "mixed" ? "Mixed" : filters.category === "special" ? "Special" : "Normal"} Stackers — ${filters.gender === "M" ? "Male" : filters.gender === "F" ? "Female" : "Combined"} — ${filters.event === "all" ? "Finals" : filters.event} Top Performance` : kind === "missing" ? "Finals Missing Results Report" : kind === "placements" ? "Finals Placement Report" : "Final Results by Division and Event";
    headers = ["Place / Rank", "Competition ID", "Participant / Team", "Member Names", "Category", "Gender", "Division", "Organization", "Country", "Region", "Event", "Attempts", "Best Valid Time", "Result Status", "Tie"];
    rows = reportRows.map(row => [row.rank || "", row.participant, row.name, row.members.map(member => member.name || member.id).join(" / "), row.special === "Yes" ? "Special" : "Normal", row.gender, row.division, row.org, row.country, row.region, row.event, row.attempts?.join(", ") || "", fmt(row.bestValidTime), row.resultStatus, row.tie ? "Equal performance" : ""]);
  }
  return { kind, title, headers, rows, contributors, filters, empty: "No matching final results." };
}

function stageReportDefinition(stage, kind, filters) {
  const allRows = FinalsReportEngine.stageResultRows(state, stage, filters);
  const events = [...new Set(allRows.map(row => row.event).filter(Boolean))].sort((a, b) => finalEventSort(a) - finalEventSort(b) || String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" }));
  const highlighted = stage === "Prelims"
    ? new Set(state.finalQualificationSnapshots.filter(snapshot => snapshot.status === "Approved").flatMap(snapshot => snapshot.selectedQualifiers.map(item => item.participantId)))
    : new Set(finalSheets().flatMap(sheet => sheet.finalists.map(finalist => finalist.participant)));
  const label = stage === "Prelims" ? "Preliminary" : "Finals";
  if (kind === "all-around") return stageAllAroundReportDefinition(stage, label, filters);
  if (["final-results", "overall", "division"].includes(kind) && filters.event === "all") return stageBoardReportDefinition(stage, kind, filters, allRows, events, highlighted, label);
  let rows = FinalsReportEngine.stagePlacementRows(state, stage, filters);
  if (kind === "missing") rows = allRows.filter(row => row.resultStatus !== "valid" || (row.result?.attempts || []).length < 3);
  if (kind === "scratch") rows = allRows.filter(row => row.resultStatus === "scratch");
  if (kind === "event" && filters.event === "all") rows = [];
  const champions = new Map();
  rows.filter(row => Number.isFinite(row.rank) && Number.isFinite(row.bestValidTime)).forEach(row => {
    const key = `${row.type}|${row.division}|${row.event}`;
    champions.set(key, Math.min(champions.get(key) ?? Infinity, row.bestValidTime));
  });
  if (kind === "division") rows.sort((a, b) => compareDivisionNames(a.division, b.division) || (a.rank || Infinity) - (b.rank || Infinity));
  const title = kind === "missing" ? `${label} Missing Results` : kind === "scratch" ? `${label} Scratch Report` : kind === "event" ? `${label} Results by Event` : kind === "division" ? `${label} Results by Division` : `${label} Results by Division and Event`;
  return { stage, kind, title, headers: ["Rank", "Stacker ID", "Name", "Organization", "Division", "Event", "Best Time", "Gap", "Status"], rows: rows.map(row => {
    const champion = champions.get(`${row.type}|${row.division}|${row.event}`);
    const gap = Number.isFinite(champion) && Number.isFinite(row.bestValidTime) && row.bestValidTime !== champion ? `+${fmt(row.bestValidTime - champion)}` : "";
    return [row.rank || "", row.participant, row.name, row.org, row.division, row.event, fmt(row.bestValidTime), gap, row.resultStatus];
  }), rowClasses: rows.map(row => highlighted.has(row.participant) ? "finals-highlight" : ""), filters, empty: kind === "event" && filters.event === "all" ? "Select one event for the judges' report." : "No matching results." };
}

function stageBoardReportDefinition(stage, kind, filters, allRows, events, highlighted, label) {
  const grouped = ["final-results", "division"].includes(kind);
  const groups = grouped
    ? [...new Set(allRows.map(row => row.division || "Unassigned"))].sort(compareDivisionNames)
    : [filters.division === "all" ? "All" : filters.division || "All"];
  const allAroundEvents = ["3-3-3", "3-6-3", "cycle"];
  const hasAllAround = allAroundEvents.every(event => events.some(item => normalizeEventName(item) === event));
  const eventSections = events.map(event => ({ title: event, event }));
  const sections = hasAllAround ? [...eventSections, { title: "All Around", event: "All Around" }] : eventSections;
  const boardGroups = groups.map(group => {
    const groupRows = grouped ? allRows.filter(row => (row.division || "Unassigned") === group) : allRows;
    return {
      title: grouped ? `Division: ${group}` : `Division: ${filters.division === "all" ? "All" : filters.division || "All"}`,
      sections: sections.map(section => ({
        title: section.title,
        rows: section.event === "All Around"
          ? stageBoardAllAroundRows(groupRows, highlighted)
          : stageBoardEventRows(groupRows.filter(row => normalizeEventName(row.event) === normalizeEventName(section.event)), highlighted)
      }))
    };
  }).filter(group => group.sections.some(section => section.rows.length));
  const csvRows = boardGroups.flatMap(group => group.sections.flatMap(section => section.rows.map(row => [
    group.title.replace(/^Division: /, ""),
    section.title,
    row.rank,
    row.participant,
    row.name,
    row.org,
    row.division,
    row.time,
    row.gap,
    row.status
  ])));
  const title = kind === "overall" ? `${label} Overall Results` : kind === "division" ? `${label} Results by Division` : `${label} Results by Division and Event`;
  return {
    stage,
    kind,
    title,
    boardGroups,
    headers: ["Division", "Event", "Rank", "Stacker ID", "Name", "Organization", "Division", "Best Time", "Gap", "Status"],
    rows: csvRows,
    csvHeaders: ["Report Division", "Event", "Rank", "Stacker ID", "Name", "Organization", "Participant Division", "Best Time", "Gap", "Status"],
    csvRows,
    filters,
    empty: "No matching results."
  };
}

function stageAllAroundReportDefinition(stage, label, filters) {
  const allAroundRows = FinalsReportEngine.stageAllAroundRows(state, stage, filters);
  const ranked = FinalsReportEngine.rankFinalRows(allAroundRows.filter(row => row.resultStatus === "valid"));
  const ineligible = allAroundRows.filter(row => row.resultStatus !== "valid");
  const rows = [...ranked, ...ineligible];
  return {
    stage,
    kind: "all-around",
    title: `${label} All-Around`,
    headers: ["Rank", "Competition ID", "Name", "Category", "Gender", "Division", "3-3-3", "3-6-3", "Cycle", "All-Around Total", "Status"],
    rows: rows.map(row => [
      row.rank || "",
      row.participant,
      row.name,
      row.special === "Yes" ? "Special" : "Normal",
      row.gender,
      row.division,
      fmt(row.events?.["3-3-3"]?.bestValidTime),
      fmt(row.events?.["3-6-3"]?.bestValidTime),
      fmt(row.events?.cycle?.bestValidTime),
      fmt(row.bestValidTime),
      row.resultStatus
    ]),
    filters,
    empty: "No matching individual all-around results."
  };
}

function stageBoardEventRows(rows, highlighted) {
  const ranked = FinalsReportEngine.rankFinalRows(rows.filter(row => row.resultStatus === "valid"));
  const champion = ranked[0]?.bestValidTime;
  return ranked.map(row => stageBoardDisplayRow(row, champion, highlighted));
}

function stageBoardAllAroundRows(rows, highlighted) {
  const byParticipant = new Map();
  rows.filter(row => row.type === "Individual").forEach(row => {
    const key = row.participant;
    if (!byParticipant.has(key)) byParticipant.set(key, { ...row, byEvent: {} });
    const event = normalizeEventName(row.event);
    const old = byParticipant.get(key).byEvent[event];
    if (row.resultStatus === "valid" && (!old || row.bestValidTime < old.bestValidTime)) byParticipant.get(key).byEvent[event] = row;
  });
  const totals = [...byParticipant.values()].map(row => {
    const required = ["3-3-3", "3-6-3", "cycle"];
    const valid = required.every(event => row.byEvent[event]?.resultStatus === "valid");
    const bestValidTime = valid ? required.reduce((sum, event) => sum + row.byEvent[event].bestValidTime, 0) : null;
    return { ...row, event: "All Around", bestValidTime, resultStatus: valid ? "valid" : "incomplete", tieKey: [bestValidTime ?? Infinity, Infinity, Infinity] };
  });
  const ranked = FinalsReportEngine.rankFinalRows(totals.filter(row => row.resultStatus === "valid"));
  const champion = ranked[0]?.bestValidTime;
  return ranked.map(row => stageBoardDisplayRow(row, champion, highlighted));
}

function stageBoardDisplayRow(row, champion, highlighted) {
  const gap = Number.isFinite(champion) && Number.isFinite(row.bestValidTime) && row.bestValidTime !== champion ? `+${fmt(row.bestValidTime - champion)}` : "";
  return {
    rank: row.rank || "",
    participant: row.participant,
    name: row.name,
    org: row.org || "--",
    division: row.division || "--",
    time: fmt(row.bestValidTime),
    gap,
    status: row.resultStatus,
    highlight: highlighted.has(row.participant)
  };
}

function finalsReportMeta(definition) {
  const filters = definition.filters;
  return [["Report header", brandText("reportHeader")], ["Active competition", state.settings.name], ["Stage", definition.stage || "Finals"], ["Report", definition.title], ["Filters", `Type: ${filters.participantType}; Division: ${filters.division}; Event: ${filters.event}; Category: ${filters.category}; Gender: ${filters.gender || "Combined"}; Org: ${filters.org || "Any"}; Country: ${filters.country || "Any"}; Region: ${filters.region || "Any"}`], ["Data as of", stackMeetDateTime()], ["Returned rows", definition.rows.length]];
}

function finalsReportPrintFilterSummary(definition) {
  const filters = definition.filters || {};
  const category = filters.category === "special" ? "Special Stackers" : filters.category === "normal" ? "Normal Stackers" : "Mixed Stackers (Normal + Special)";
  const gender = filters.gender === "M" ? "Male" : filters.gender === "F" ? "Female" : "Combined (Male + Female)";
  return `Filter: ${category} / ${gender}`;
}

function runFinalsReport() {
  const output = document.getElementById("finalsReportOutput");
  if (!output) return;
  let definition;
  try {
    definition = finalsReportDefinition();
  } catch (error) {
    console.error("Unable to build competition report.", error);
    output.innerHTML = `<div class="report-empty"><strong>Unable to build report.</strong><br>${esc(error?.message || error)}</div>`;
    return;
  }
  const qualificationActions = definition.kind === "qualification" ? `<button class="ghost" data-action="generate-qualification-snapshots" type="button">Generate Draft Qualification Snapshots</button>` : "";
  const table = definition.rows.map((row, index) => `<tr${definition.rowClasses?.[index] ? ` class="${definition.rowClasses[index]}"` : ""}>${row.map(cell => `<td>${esc(cell)}</td>`).join("")}</tr>`).join("") || `<tr><td colspan="${definition.headers.length}">${esc(definition.empty)}</td></tr>`;
  const drilldown = definition.contributors ? `<details class="report-drilldown"><summary>Organization placement contributors</summary>${definition.contributors.map(org => `<h3>${esc(org.organization)}</h3><ul>${org.placements.map(item => `<li>${esc(`${item.name} — ${item.type} — ${item.division} — ${item.event} — Place ${item.rank} — ${fmt(item.bestValidTime)} — credited to ${item.representedOrganization}`)}</li>`).join("")}</ul>`).join("")}</details>` : "";
  const drafts = definition.kind === "qualification" ? state.finalQualificationSnapshots.filter(snapshot => snapshot.status === "Draft").map(snapshot => `<li>${esc(`${snapshot.participantType} / ${snapshot.division} / ${snapshot.event}: ${snapshot.tieException?.required ? "exception required" : "ready"}`)} ${snapshot.tieException?.required ? "" : `<button class="ghost compact-button" data-action="approve-qualification-snapshot" data-id="${esc(snapshot.id)}" type="button">Approve</button>`}</li>`).join("") : "";
  if (definition.boardGroups) {
    output.innerHTML = `<div class="report-document report-board-document" data-report-kind="finals"><div class="report-actions no-print">${qualificationActions}<button class="ghost" data-action="print-finals-report" type="button">Print</button><button class="ghost" data-action="export-finals-csv" type="button">Export CSV</button></div><header class="report-header" data-print-filter=""><p>${esc(brandText("reportHeader"))}</p><h2>${esc(definition.title)}</h2><strong>${esc(state.settings.name)}${state.settings.start ? ` - ${esc(state.settings.start)}` : ""}</strong></header><div class="report-meta">${finalsReportMeta(definition).map(([label, value]) => `<div><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join("")}</div>${drafts ? `<div class="report-note"><strong>Draft qualification snapshots</strong><ul>${drafts}</ul></div>` : ""}${stageBoardReportHtml(definition)}${drilldown}</div>`;
    return;
  }
  output.innerHTML = `<div class="report-document" data-report-kind="finals"><div class="report-actions no-print">${qualificationActions}<button class="ghost" data-action="print-finals-report" type="button">Print</button><button class="ghost" data-action="export-finals-csv" type="button">Export CSV</button></div><header class="report-header"><p>${esc(brandText("reportHeader"))}</p><h2>${esc(definition.title)}</h2><strong>${esc(state.settings.name)}${state.settings.start ? ` — ${esc(state.settings.start)}` : ""}</strong></header><div class="report-meta">${finalsReportMeta(definition).map(([label, value]) => `<div><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join("")}</div>${drafts ? `<div class="report-note"><strong>Draft qualification snapshots</strong><ul>${drafts}</ul></div>` : ""}<div class="table-wrap report-table-wrap"><table><thead><tr>${definition.headers.map(header => `<th>${esc(header)}</th>`).join("")}</tr></thead><tbody>${table}</tbody></table></div>${drilldown}</div>`;
  decorateFinalsReportForPrint(output, definition);
}

function decorateFinalsReportForPrint(output, definition) {
  if (definition.kind !== "all-around") return;
  const documentElement = output.querySelector(".report-document");
  const header = output.querySelector(".report-header");
  documentElement?.classList.add("report-all-around-document");
  header?.setAttribute("data-print-filter", finalsReportPrintFilterSummary(definition));
}

function stageBoardReportHtml(definition) {
  if (!definition.boardGroups.length) return `<div class="report-empty">${esc(definition.empty)}</div>`;
  return `<div class="stage-board-report">${definition.boardGroups.map(group => `
    <section class="stage-board-group">
      <header class="stage-board-print-header">
        <p>${esc(brandText("reportHeader"))}</p>
        <h2>${esc(definition.title)}</h2>
        <strong>${esc(state.settings.name)}${state.settings.start ? ` - ${esc(state.settings.start)}` : ""}</strong>
      </header>
      <h3>${esc(group.title)}</h3>
      <div class="stage-board-grid">
        ${group.sections.map(section => `
          <article class="stage-board-section">
            <h4>${esc(section.title)}</h4>
            <div class="stage-board-header"><span>#</span><span>Stacker</span><span>Time</span><span>Gap</span></div>
            ${section.rows.length ? section.rows.map(row => `
              <div class="stage-board-row${row.highlight ? " finals-highlight" : ""}">
                <span>${esc(row.rank)}</span>
                <span><strong>${esc(row.name)} <small>(ID:${esc(row.participant)})</small></strong><small>${esc(row.org)}${row.division ? ` / ${esc(row.division)}` : ""}</small></span>
                <span>${esc(row.time)}</span>
                <span>${esc(row.gap)}</span>
              </div>
            `).join("") : `<div class="stage-board-empty">No results</div>`}
          </article>
        `).join("")}
      </div>
    </section>
  `).join("")}</div>`;
}

function generateQualificationSnapshots() {
  const sheets = finalSheets();
  const approved = state.finalQualificationSnapshots.filter(snapshot => snapshot.status === "Approved");
  if (approved.length && !confirm("Approved qualification snapshots will be superseded and preserved. Create new Draft snapshots?")) return;
  if (approved.length) state.finalQualificationSnapshots = state.finalQualificationSnapshots.map(snapshot => snapshot.status === "Approved" ? { ...snapshot, status: "Superseded" } : snapshot);
  const snapshots = sheets.map(sheet => FinalsReportEngine.qualificationSnapshot(state, sheet, { id: crypto.randomUUID(), competitionKey: currentCompetitionKey(), limit: finalAdvanceLimit(sheet.type, sheet.division) }));
  state.finalQualificationSnapshots.push(...snapshots);
  runFinalsReport();
}

function approveQualificationSnapshot(id) {
  const snapshot = state.finalQualificationSnapshots.find(item => item.id === id);
  if (!snapshot || snapshot.status !== "Draft" || snapshot.tieException?.required) return;
  state.finalQualificationSnapshots = state.finalQualificationSnapshots.map(item => item.id === id ? { ...item, status: "Approved", approvedAtUtc: new Date().toISOString(), approvedBy: "" } : item);
  runFinalsReport();
}

function exportFinalsCsv() {
  const definition = finalsReportDefinition();
  downloadText("NADITrack-finals-report.csv", [[brandText("reportHeader")], [state.settings.name], [definition.title], [], definition.csvHeaders || definition.headers, ...(definition.csvRows || definition.rows)].map(csvLine).join("\n"), "text/csv");
}

function printFinalsReport() {
  document.body.dataset.printTarget = "finals-report";
  window.addEventListener("afterprint", () => delete document.body.dataset.printTarget, { once: true });
  window.print();
}

function renderLeaderboard() {
  stopLeaderboardLoop();
  const board = document.getElementById("leaderDisplay");
  board.classList.remove("light");
  board.style.fontSize = `${leaderboardFontScale()}rem`;
  board.style.setProperty("--leader-progress-color", leaderboardProgressColor());
  board.style.setProperty("--leader-progress-height", `${leaderboardProgressHeight()}px`);
  const slides = leaderboardSlides();
  leaderboardSlideIndex = Math.min(leaderboardSlideIndex, Math.max(0, slides.length - 1));
  renderLeaderboardSlide(slides, leaderboardSlideIndex);
  scheduleLeaderboardSlide(slides);
}

function stopLeaderboardLoop() {
  if (!leaderboardTimer) return;
  clearTimeout(leaderboardTimer);
  leaderboardTimer = null;
}

function scheduleLeaderboardSlide(slides) {
  if (slides.length <= 1) return;
  const slide = slides[leaderboardSlideIndex] || emptyLeaderboardSlide();
  leaderboardTimer = setTimeout(() => {
    if (route !== "leaderboard") return stopLeaderboardLoop();
    leaderboardSlideIndex = (leaderboardSlideIndex + 1) % slides.length;
    renderLeaderboardSlide(slides, leaderboardSlideIndex);
    scheduleLeaderboardSlide(slides);
  }, leaderboardPauseMs(slide));
}

function leaderboardPauseMs(slide = null) {
  if (slide && !slide.rows.length) return 2000;
  const seconds = clampNumber(numericFromSetting(state.leaderboard.pause), 8, 1, 300);
  return Math.max(1, seconds) * 1000;
}

function renderLeaderboardSlide(slides, index) {
  const slide = slides[index] || emptyLeaderboardSlide();
  const nextSlide = slides.length > 1 ? slides[(index + 1) % slides.length] : null;
  const durationMs = leaderboardPauseMs(slide);
  document.getElementById("leaderCaption").textContent = slide.caption;
  document.getElementById("leaderTitle").textContent = slide.title;
  document.getElementById("leaderRows").innerHTML = slide.rows.length ? `
    <div class="leader-progress" style="animation-duration: ${esc(durationMs)}ms"></div>
    <div class="leader-subtitle"><span>${esc(slide.subtitle)}</span><span>${esc(index + 1)} / ${esc(slides.length)}</span></div>
    <div class="leader-header"><span></span><span>Stacker</span><span>Time</span><span>Gap</span></div>
    ${slide.rows.map(row => `
      <div class="leader-row">
        <div class="rank">${esc(row.rank)}</div>
        <div><strong>${esc(row.name)}</strong><br><small>${esc(row.meta)}</small></div>
        <strong>${esc(row.time)}</strong>
        <span>${esc(row.gap)}</span>
      </div>
    `).join("")}
    <footer class="leader-footer"><span>Results are not final, times/rankings may change.</span><span>${nextSlide ? `Next: ${esc(nextSlide.title)}` : ""}</span></footer>
  ` : `
    <div class="leader-progress" style="animation-duration: ${esc(durationMs)}ms"></div>
    <div class="leader-empty">${esc(slide.subtitle || "No results yet.")}</div>
    <footer class="leader-footer"><span>Results are not final, times/rankings may change.</span><span>${nextSlide ? `Next: ${esc(nextSlide.title)}` : ""}</span></footer>
  `;
}

function leaderboardFontScale() {
  return clampNumber(numericFromSetting(state.leaderboard.fontSize), 1, 0.5, 2);
}

function leaderboardProgressHeight() {
  return clampNumber(numericFromSetting(state.leaderboard.progressHeight), 4, 1, 20);
}

function leaderboardProgressColor() {
  const color = String(state.leaderboard.color || "Blue").toLowerCase();
  if (color === "red") return "#ef4444";
  if (color === "green") return "#22c55e";
  return "#65d7ff";
}

function emptyLeaderboardSlide() {
  return { caption: `${state.leaderboard.stage} - ${state.leaderboard.type}`, title: state.settings.name, subtitle: "No results yet.", rows: [] };
}

function leaderboardSlides() {
  if (state.leaderboard.type === "Tournament Logo") {
    return [{ caption: "Tournament", title: state.settings.name, subtitle: brandText("reportHeader"), rows: [] }];
  }
  const stage = state.leaderboard.type === "SOC" ? "SOC" : state.leaderboard.stage;
  const rows = state.results
    .filter(result => leaderboardStageMatches(result.stage, stage) && Number.isFinite(official(result)))
    .map(result => competitionRowFromResult(result))
    .filter(row => Number.isFinite(row.time));
  if (!rows.length) return [emptyLeaderboardSlide()];
  const grouped = leaderboardGroupedRows(rows);
  return grouped.map(group => ({
    caption: state.settings.name,
    title: leaderboardSlideTitle(stage, group),
    subtitle: group.division === "Overall" ? state.leaderboard.type : group.division,
    rows: leaderboardRankRows(group.rows).slice(0, Number(state.leaderboard.limit) || 10)
  }));
}

function leaderboardStageMatches(resultStage, selectedStage) {
  return String(resultStage || "").toLowerCase() === String(selectedStage || "").toLowerCase();
}

function leaderboardGroupedRows(rows) {
  const divisionMode = state.leaderboard.type === "Divisional Results";
  const groups = rows.reduce((acc, row) => {
    const category = leaderboardCategory(row);
    const division = divisionMode ? row.division || "Open" : "Overall";
    const key = `${division}|${category}`;
    if (!acc.has(key)) acc.set(key, { division, category, rows: [] });
    acc.get(key).rows.push(row);
    return acc;
  }, new Map());
  return [...groups.values()]
    .sort((left, right) => compareDivisionNames(left.division, right.division) || leaderboardCategorySort(left.category) - leaderboardCategorySort(right.category) || left.category.localeCompare(right.category, undefined, { numeric: true, sensitivity: "base" }))
    .map(group => ({
      division: group.division,
      category: group.category,
      rows: group.rows
    }));
}

function leaderboardCategory(row) {
  const type = row.type === "Timed Relay" ? "Relay" : row.type;
  return `${type} - ${row.event}`;
}

function leaderboardSlideTitle(stage, group) {
  const [type, ...eventParts] = String(group.category || "").split(" - ");
  const event = eventParts.join(" - ");
  const typeLabel = type === "Individual" ? "Individuals" : type;
  const stageLabel = stage === "Prelims" ? "Prelims" : stage === "Finals" ? "Finals" : stage;
  return [stageLabel, typeLabel, group.division === "Overall" ? "" : group.division, event].filter(Boolean).join(" // ");
}

function leaderboardCategorySort(category) {
  const text = String(category || "");
  const typeOrder = text.startsWith("Individual") ? 1 : text.startsWith("Doubles") ? 2 : text.startsWith("Relay") ? 3 : 9;
  const event = text.split(" - ").slice(1).join(" - ");
  return typeOrder * 10 + finalEventSort(event);
}

function leaderboardRankRows(rows) {
  const bestByParticipant = new Map();
  rows.forEach(row => {
    const current = bestByParticipant.get(row.participant);
    if (!current || compareCompetitionRows(row, current) < 0) bestByParticipant.set(row.participant, row);
  });
  const sorted = [...bestByParticipant.values()].sort(compareCompetitionRows);
  let previousTime = NaN;
  let previousRank = 0;
  const leaderTime = sorted[0]?.time;
  return sorted.map((row, index) => {
    const rank = row.time === previousTime ? previousRank : index + 1;
    previousTime = row.time;
    previousRank = rank;
    return {
      rank,
      name: row.name,
      meta: [row.org || row.country || "", row.participant ? `ID ${row.participant}` : ""].filter(Boolean).join(" / "),
      time: leaderboardTime(row.time),
      gap: row.time === leaderTime ? "" : `+${leaderboardTime(row.time - leaderTime)}`
    };
  });
}

function leaderboardTime(value) {
  return Number.isFinite(value) ? value.toFixed(3) : "--";
}

function renderUsers() {
  document.getElementById("userRows").innerHTML = state.users.map(u => `
    <tr><td><strong>${esc(u.name)}</strong></td><td>${esc(u.access)}</td><td>${esc(u.last)}</td><td>${esc(u.platform)}</td><td>${esc(u.browser)}</td></tr>
  `).join("");
}

function updateStackerDivisionPreview() {
  const field = document.getElementById("stDivision");
  const ageField = document.getElementById("stAge");
  if (!field) return;
  const age = ageOnCompetitionDate(val("stDob"), state.settings.start);
  if (ageField) ageField.value = age || "";
  const division = stackerDivisionFromForm();
  const editedStacker = state.stackers.find(stacker => stacker.id === editingStackerId);
  field.value = division || editedStacker?.division || "";
}

function stackerDivisionFromForm() {
  const custom = val("stCustomDivision").trim();
  if (custom) return custom;
  const editedStacker = state.stackers.find(stacker => stacker.id === editingStackerId);
  if (editedStacker?.standardDivision) return editedStacker.standardDivision;
  const age = ageOnCompetitionDate(val("stDob"), state.settings.start);
  const gender = val("stGender");
  return findDivisionFor(age, gender, isSpecialStacker(), state.divisionSettings, state.settings.separateSpecialDivisionsByGender === true) || "";
}

function divisionForStacker(stacker, divisionSettings, competitionStart, separateSpecialDivisionsByGender = false) {
  const settings = divisionSettings || state.divisionSettings;
  const start = competitionStart || state.settings.start;
  if (stacker.customDivision) return stacker.customDivision;
  if (stacker.standardDivision) return stacker.standardDivision;
  const age = ageOnCompetitionDate(stacker.dob, start) || Number(stacker.age);
  return findDivisionFor(age, stacker.gender, stacker.special === "Yes", settings, separateSpecialDivisionsByGender) || "";
}

function findDivisionFor(age, gender, special = false, divisionSettings, separateSpecialDivisionsByGender = false) {
  const settings = divisionSettings || state.divisionSettings;
  if (!Number.isFinite(age) || age <= 0) return "";
  if (special) {
    const baseDivision = findRangeName(age, settings.special || [], "Special") || "SS";
    return separateSpecialDivisionsByGender ? `${baseDivision} ${gender === "F" ? "F" : "M"}` : baseDivision;
  }
  const genderKey = gender === "F" ? "female" : "male";
  const label = gender === "F" ? "Female" : "Male";
  return findRangeName(age, divisionPath(settings, genderKey, label));
}

function findRangeName(age, cutoffsOrPath, fallbackLabel = "") {
  let previous = 0;
  const path = cutoffsOrPath
    .map(item => typeof item === "number" ? { age: item, label: fallbackLabel } : item)
    .sort((a, b) => a.age - b.age);
  for (const item of path) {
    const cutoff = item.age;
    const label = item.label;
    const start = previous + 1;
    if (age <= cutoff) {
      if (label === "Special") {
        if (start <= 4) return `SS ${cutoff} & Under L1`;
        if (start === cutoff) return `SS ${cutoff} L1`;
        return `SS ${start}-${cutoff} L1`;
      }
      if (label === "Combined") {
        const standardName = standardCombinedDivisionName(start, cutoff);
        if (standardName) return standardName;
        if (age >= 19) {
          const adultName = standardCombinedDivisionName(age, age);
          if (adultName) return adultName;
        }
      }
      if (start <= 4) return `${cutoff} & Under ${label}`;
      if (start === cutoff) return `${cutoff} ${label}`;
      return `${start}-${cutoff} ${label}`;
    }
    previous = cutoff;
  }
  return "";
}

function divisionPath(settings, genderKey, genderLabel) {
  const byAge = new Map();
  (settings[genderKey] || []).forEach(age => byAge.set(Number(age), genderLabel));
  (settings.combined || []).forEach(age => byAge.set(Number(age), "Combined"));
  return [...byAge.entries()]
    .filter(([age]) => Number.isFinite(age))
    .map(([age, label]) => ({ age, label }))
    .sort((a, b) => a.age - b.age);
}

function isSpecialStacker() {
  return document.getElementById("stSpecial")?.checked || false;
}

function recalculateStackerDivisions(stackers, divisionSettings, competitionStart, separateSpecialDivisionsByGender = false) {
  const settings = divisionSettings || state.divisionSettings;
  const start = competitionStart || state.settings.start;
  return stackers.map(stacker => {
    const age = ageOnCompetitionDate(stacker.dob, start) || stacker.age || "";
    const updated = { ...stacker, age };
    const generatedDivision = divisionForStacker(updated, settings, start, separateSpecialDivisionsByGender);
    const hasUsableAge = Number(age) > 0;
    return {
      ...updated,
      division: generatedDivision || (hasUsableAge ? "Open" : updated.division || "Open")
    };
  });
}

function syncStackerEditState() {
  const saveButton = document.getElementById("saveStackerBtn");
  const cancelButton = document.getElementById("cancelStackerEdit");
  const printButton = document.getElementById("printStackerSheetBtn");
  if (saveButton) saveButton.textContent = editingStackerId ? `Update ${editingStackerId}` : "Save Stacker";
  if (cancelButton) cancelButton.hidden = false;
  const title = document.getElementById("stackerFormTitle");
  if (title) title.textContent = editingStackerId ? `Edit Stacker ${editingStackerId}` : "New Stacker";
  if (printButton) {
    printButton.hidden = !editingStackerId;
    printButton.dataset.id = editingStackerId;
  }
  updateStackerDoublesInfo();
}

function updateStackerDoublesInfo() {
  const box = document.getElementById("stDoublesInfo");
  if (!box) return;
  if (!editingStackerId) {
    box.hidden = true;
    box.innerHTML = "";
    return;
  }
  const team = doublesForStacker(editingStackerId)[0];
  const partner = team ? doublePartnerNameForStacker(team, editingStackerId) : "No doubles entry yet";
  const editorHtml = stackerDoubleEditorOpen ? `
    <div class="inline-editor">
      <label>Status<select id="stDoubleStatus"><option value="complete">Complete</option><option value="pending">Need Partner</option></select></label>
      <label>Search Registered Partner<input id="stDoublePartnerSearch" placeholder="Name or stacker ID" /></label>
      <label>Registered Partner<select id="stDoublePartner"></select></label>
      <label>Parent / Guardian<input id="stDoubleParentName" placeholder="External parent / guardian name" /></label>
      <label>Custom Division<input id="stDoubleDivision" placeholder="Auto if blank" /></label>
      <div id="stDoubleWarning" class="inline-warning" hidden></div>
      <button class="ghost compact-button" data-action="save-stacker-double" type="button">Save Doubles</button>
    </div>
  ` : "";
  box.hidden = false;
  box.innerHTML = `
    <div class="info-strip-summary">
      <strong>Doubles</strong>
      <span>ID: ${esc(team?.id || "New")}</span>
      <span>Type: ${esc(team ? doubleTypeLabel(team) : "Auto")}</span>
      <span>Status: ${esc(team ? doubleStatusLabel(team) : "Not assigned")}</span>
      <span>Partner: ${esc(partner)}</span>
      <span>Division: ${esc(team ? doubleDivision(team) : "Auto")}</span>
      <button class="ghost compact-button" data-action="edit-stacker-double" type="button">${esc(t("Edit Doubles"))}</button>
    </div>
    ${editorHtml}
  `;
  if (!stackerDoubleEditorOpen) return;
  setValue("stDoubleStatus", team?.status || "complete");
  setValue("stDoubleParentName", team?.parentName || "");
  setValue("stDoubleDivision", team?.customDivision || "");
  fillStackerPartnerSelect(team);
  document.getElementById("stDoublePartnerSearch")?.addEventListener("input", () => fillStackerPartnerSelect(team));
  document.getElementById("stDoubleStatus")?.addEventListener("change", () => fillStackerPartnerSelect(team));
  document.getElementById("stDoublePartner")?.addEventListener("change", updateStackerDoubleWarning);
  updateStackerDoubleWarning();
}

function fillStackerPartnerSelect(team = null) {
  const select = document.getElementById("stDoublePartner");
  if (!select || !editingStackerId) return;
  const currentPartner = team?.one === editingStackerId ? team.two : team?.one;
  const term = val("stDoublePartnerSearch").trim().toLowerCase();
  const options = state.stackers
    .filter(stacker => stacker.id !== editingStackerId)
    .filter(stacker => !term || stackerPickerSearchText(stacker).includes(term))
    .sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
  select.innerHTML = [`<option value="">--</option>`].concat(options.map(stacker => {
    const assigned = doublesForStacker(stacker.id).find(existing => existing.id !== team?.id);
    const className = assigned ? "assigned-option" : "";
    const status = assigned ? `Team ${assigned.id}: ${participantName("Doubles", assigned.id)}` : "Available";
    return `<option value="${esc(stacker.id)}" class="${className}">${esc(stackerPickerLabel(stacker, status))}</option>`;
  })).join("");
  if (currentPartner && [...select.options].some(option => option.value === currentPartner)) select.value = currentPartner;
  updateStackerDoubleWarning();
}

function updateStackerDoubleWarning() {
  const box = document.getElementById("stDoubleWarning");
  if (!box || !editingStackerId) return;
  const currentTeam = doublesForStacker(editingStackerId)[0];
  const partnerId = selectedStackerId("stDoublePartner");
  const assigned = partnerId ? doublesForStacker(partnerId).find(team => team.id !== currentTeam?.id) : null;
  if (!assigned) {
    box.hidden = true;
    box.textContent = "";
    return;
  }
  box.hidden = false;
  box.textContent = `${stackerName(partnerId)} is now in ${assigned.id}. Saving will remove them from that team and pair them here.`;
}

function populateParticipants() {
  const type = document.getElementById("entryType")?.value || "Individual";
  const options = type === "Doubles"
    ? completedDoubles().map(d => `${d.id} - ${participantName("Doubles", d.id)}`)
    : type === "Relay"
      ? completedRelays().map(team => `${team.id} - ${participantName("Timed Relay", team.id)}`)
      : state.stackers.map(s => `${s.id} - ${s.name}`);
  setOptions("participantSelect", options);
}

function drawResultRows() {
  const selectedStage = val("resultStage");
  const rows = [...state.results]
    .filter(result => !selectedStage || result.stage === selectedStage)
    .reverse()
    .slice(0, 12);
  document.getElementById("resultRows").innerHTML = rows.map(r => `
    <tr>
      <td>${esc(r.stage)}</td><td><strong>${esc(participantName(r.type, r.participant))}</strong></td><td>${esc(r.event)}</td><td>${fmt(bestAttempt(r))}</td><td>${esc(resultStatusLabel(r))}</td>
      <td><button class="icon-button" data-action="delete-result" data-id="${esc(r.id)}" type="button">x</button></td>
    </tr>
  `).join("") || `<tr><td colspan="6"><span class="muted">No ${esc(selectedStage || "")} results yet.</span></td></tr>`;
}

function drawMissingTimes() {
  const groups = missingPrelimGroups();
  const totalMissing = groups.reduce((total, group) => total + group.items.length, 0);
  const summary = `<div class="missing-summary">${groups.map(group => `<div><span>${esc(group.summaryLabel)}</span><strong>${group.items.length}</strong></div>`).join("")}</div>`;
  const lists = groups.map(group => `
    <section class="missing-group">
      <h3>${esc(group.label)}</h3>
      <div class="missing-entry-list">${group.items.map(item => `
        <button class="missing-entry-button" data-action="load-missing-prelim" data-id="${esc(item.id)}" type="button">
          <span><strong>${esc(item.id)}</strong>${esc(item.name)}</span>
          <small>${esc(item.missingEvents.join(", "))}</small>
        </button>`).join("") || `<p class="muted">Complete</p>`}</div>
    </section>`).join("");
  document.getElementById("missingTimes").innerHTML = `${summary}${totalMissing ? `<div class="missing-groups">${lists}</div>` : `<div class="list-row"><strong>All required prelim times entered</strong></div>`}`;
}

function missingPrelimGroups() {
  const definitions = [
    {
      label: "Individuals",
      summaryLabel: "Individual Missing",
      type: "Individual",
      events: prelimEventsForParticipant(prelimEntryConfig["1"]),
      participants: state.stackers.map(stacker => ({ id: stacker.id, name: stacker.name }))
    },
    {
      label: "Doubles",
      summaryLabel: "Doubles Missing",
      type: "Doubles",
      events: prelimEventsForParticipant(prelimEntryConfig["2"]),
      participants: completedDoubles().map(team => ({ id: team.id, name: participantName("Doubles", team.id) }))
    },
    {
      label: "Relay Teams",
      summaryLabel: "Relay Team Missing",
      type: "Timed Relay",
      events: prelimEventsForParticipant(prelimEntryConfig["3"]),
      participants: completedRelays().map(team => ({ id: team.id, name: participantName("Timed Relay", team.id) }))
    }
  ];
  return definitions.map(definition => ({
    ...definition,
    items: definition.participants
      .map(participant => ({
        ...participant,
        missingEvents: definition.events.filter(event => !state.results.some(result => prelimEntryLookupStages().includes(result.stage) && result.type === definition.type && result.participant === participant.id && normalizeEventName(result.event) === normalizeEventName(event)))
      }))
      .filter(participant => participant.missingEvents.length)
      .sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id))
  }));
}

function loadMissingPrelim(id) {
  setValue("resultStage", "Prelims");
  updateCompetitionEntryMode();
  setValue("timeSheetId", id);
  loadPrelimParticipant();
}

function populateCompetitionReportBuilder() {
  populateReportSelect("competitionCountry", uniqueReportValues(state.stackers, "country"));
  populateReportSelect("competitionRegion", uniqueReportValues(state.stackers, "region"));
  populateReportSelect("competitionOrg", uniqueReportValues(state.stackers, "org"));
  const divisionSelect = document.getElementById("competitionDivision");
  if (divisionSelect) {
    const current = divisionSelect.value || "all";
    const divisions = sortedDivisions(["Collegiate C", "Masters 1-4 C", ...state.divisions, ...state.stackers.map(stacker => stacker.division)]);
    divisionSelect.innerHTML = [
      `<option value="all">All (by Overall)</option>`,
      `<option value="all-div">All (by Division)</option>`,
      ...divisions.map(division => `<option value="${esc(division)}">${esc(division)}</option>`)
    ].join("");
    if ([...divisionSelect.options].some(option => option.value === current)) divisionSelect.value = current;
  }
  const limitSelect = document.getElementById("competitionLimit");
  if (limitSelect && !limitSelect.options.length) {
    limitSelect.innerHTML = [`<option value="0">No Limit</option>`, ...Array.from({ length: 25 }, (_, index) => {
      const value = index + 1;
      return `<option value="${value}">Top ${value}</option>`;
    })].join("");
  }
}

function runCompetitionReport() {
  const out = document.getElementById("competitionReportOutput");
  if (!out) return;
  const rows = competitionReportRows();
  const title = competitionReportTitle(rows.length);
  out.innerHTML = competitionReportTable(title, rows);
}

function competitionReportRows() {
  const type = val("competitionTypeReport") || "i";
  const event = val("competitionEvent") || "all";
  const rawRows = event === "all"
    ? [...eventRows(type, "all-min"), ...(type === "i" ? allAroundRows(type) : [])]
    : event === "all-around" ? allAroundRows(type) : eventRows(type, event);
  const filtered = applyCompetitionFilters(rawRows);
  const divisionMode = val("competitionDivision") || "all";
  const limit = Number(val("competitionLimit")) || 0;
  const grouped = divisionMode === "all-div" || val("competitionSpecialMode") === "separate";
  if (grouped) {
    return rankCompetitionRows(limitGroupedRows(filtered, competitionGroupKey, limit), true);
  }
  const sorted = filtered.sort(compareCompetitionRows);
  return rankCompetitionRows(limit ? sorted.slice(0, limit) : sorted, false);
}

function eventRows(type, eventFilter) {
  const typeName = competitionResultTypeName(type);
  const allowedEvents = competitionAllowedEvents(type, eventFilter);
  return state.results
    .filter(result => result.type === typeName && competitionStageMatches(result.stage) && allowedEvents.includes(normalizeEventName(result.event)))
    .map(result => competitionRowFromResult(result))
    .filter(row => Number.isFinite(row.time));
}

function allAroundRows(type) {
  const typeName = competitionResultTypeName(type);
  const byParticipant = {};
  state.results
    .filter(result => result.type === typeName && competitionStageMatches(result.stage) && ["3-3-3", "3-6-3", "cycle"].includes(normalizeEventName(result.event)))
    .forEach(result => {
      const key = result.participant;
      if (!byParticipant[key]) byParticipant[key] = {};
      const event = normalizeEventName(result.event);
      const time = official(result);
      if (Number.isFinite(time)) byParticipant[key][event] = Math.min(byParticipant[key][event] || Infinity, time);
    });
  return Object.entries(byParticipant)
    .map(([participant, events]) => {
      if (!["3-3-3", "3-6-3", "cycle"].every(event => Number.isFinite(events[event]))) return null;
      const fakeResult = { type: typeName, participant, event: "All Around", stage: "All", attempts: [events["3-3-3"] + events["3-6-3"] + events.cycle], penalty: 0 };
      return competitionRowFromResult(fakeResult, "All Around");
    })
    .filter(Boolean);
}

function competitionRowFromResult(result, eventOverride = "") {
  const meta = competitionParticipantMeta(result.type, result.participant);
  const time = official(result);
  return {
    rank: 0,
    type: result.type,
    participant: result.participant,
    event: eventOverride || result.event,
    stage: result.stage,
    time,
    sortKey: result.stage === "Finals" ? finalTieBreakKey(result) : [time, Infinity, Infinity],
    gap: 0,
    name: meta.name,
    org: meta.org,
    country: meta.country,
    region: meta.region,
    gender: meta.gender,
    division: meta.division,
    special: meta.special
  };
}

function competitionParticipantMeta(typeName, participantId) {
  if (typeName === "Doubles") {
    const team = findDoublesTeam(participantId) || {};
    const members = registeredDoubleMemberIds(team).map(memberId => state.stackers.find(stacker => stacker.id === memberId) || {});
    return {
      name: participantName("Doubles", participantId),
      org: members.find(member => member.org)?.org || team.org || "",
      country: teamCountry(team),
      region: team.region || members.find(member => member.region)?.region || "",
      gender: "",
      division: doubleDivision(team),
      special: members.some(member => member.special === "Yes") ? "Yes" : "No"
    };
  }
  if (typeName === "Timed Relay" || typeName === "Relay") {
    const team = state.relays.find(item => item.id === participantId) || {};
    const members = relayMemberIds(team).map(id => state.stackers.find(stacker => stacker.id === id) || {});
    return {
      name: participantName("Timed Relay", participantId),
      org: team.org || members.find(member => member.org)?.org || "",
      country: team.country || members.find(member => member.country)?.country || "",
      region: team.region || members.find(member => member.region)?.region || "",
      gender: "",
      division: team.division || "",
      special: members.some(member => member.special === "Yes") ? "Yes" : "No"
    };
  }
  const stacker = state.stackers.find(item => item.id === participantId) || {};
  return {
    name: stacker.name || participantId,
    org: stacker.org || "",
    country: stacker.country || "",
    region: stacker.region || "",
    gender: stacker.gender || "",
    division: stacker.division || "",
    special: stacker.special || "No"
  };
}

function applyCompetitionFilters(rows) {
  let filtered = [...rows];
  const division = val("competitionDivision");
  if (division && !["all", "all-div"].includes(division)) filtered = filtered.filter(row => row.division === division);
  filtered = applyCompetitionFilter(filtered, "gender", val("competitionGender"), val("competitionGenderOp"));
  filtered = applyCompetitionFilter(filtered, "country", val("competitionCountry"), val("competitionCountryOp"));
  filtered = applyCompetitionFilter(filtered, "region", val("competitionRegion"), val("competitionRegionOp"));
  filtered = applyCompetitionFilter(filtered, "org", val("competitionOrg"), val("competitionOrgOp"));
  const specialMode = val("competitionSpecialMode") || "combined";
  if (specialMode === "special") filtered = filtered.filter(isCompetitionSpecialRow);
  if (specialMode === "normal") filtered = filtered.filter(row => !isCompetitionSpecialRow(row));
  return filtered;
}

function applyCompetitionFilter(rows, key, value, op) {
  if (!value) return rows;
  return rows.filter(row => (op === "!=") !== (String(row[key] || "") === value));
}

function limitGroupedRows(rows, keyGetter, limit) {
  const groups = rows.reduce((acc, row) => {
    const group = typeof keyGetter === "function" ? keyGetter(row) : row[keyGetter];
    const key = group || "Blank";
    if (!acc[key]) acc[key] = [];
    acc[key].push(row);
    return acc;
  }, {});
  return Object.entries(groups)
    .sort(([a], [b]) => compareDivisionNames(a, b))
    .flatMap(([, groupRows]) => {
      const sorted = groupRows.sort(compareCompetitionRows);
      return limit ? sorted.slice(0, limit) : sorted;
    });
}

function compareCompetitionRows(a, b) {
  const left = a.sortKey || [a.time, Infinity, Infinity];
  const right = b.sortKey || [b.time, Infinity, Infinity];
  return left[0] - right[0] || left[1] - right[1] || left[2] - right[2] || String(a.name).localeCompare(String(b.name), undefined, { sensitivity: "base" });
}

function rankCompetitionRows(rows, groupedByDivision) {
  let lastGroup = "";
  let bestInGroup = 0;
  let rankInGroup = 0;
  return rows.map((row, index) => {
    const group = groupedByDivision ? competitionGroupKey(row) : "Overall";
    if (group !== lastGroup) {
      lastGroup = group;
      bestInGroup = row.time;
      rankInGroup = 0;
    }
    rankInGroup += 1;
    return {
      ...row,
      rank: groupedByDivision ? rankInGroup : index + 1,
      gap: row.time - bestInGroup
    };
  });
}

function competitionReportTable(title, rows) {
  const groupedByDivision = val("competitionDivision") === "all-div" || val("competitionSpecialMode") === "separate";
  const highlight = val("competitionHighlight") === "yes";
  const advanceLimit = competitionAdvanceLimit();
  let lastGroup = "";
  const body = rows.map((row, index) => {
    const group = groupedByDivision ? competitionGroupKey(row) : "Overall";
    const groupHeader = group !== lastGroup ? `<tr class="group-row"><td colspan="6">${esc(group)}</td></tr>` : "";
    if (group !== lastGroup) {
      lastGroup = group;
    }
    const finalClass = highlight && advanceLimit && row.rank <= advanceLimit ? ` class="finals-highlight"` : "";
    return `${groupHeader}<tr${finalClass}><td>${row.rank}</td><td>${esc(row.event)}</td><td><strong>${esc(row.name)}</strong><small>${esc(row.org || row.country || "")}</small></td><td>${esc(row.division)}</td><td>${fmt(row.time)}</td><td>${row.gap ? `+${fmt(row.gap)}` : ""}</td></tr>`;
  }).join("");
  const exportButtons = `<div class="report-export-actions"><button class="ghost" data-action="export-results-json" type="button">Export JSON</button><button class="ghost" data-action="export-results-csv" type="button">Export CSV</button><button class="ghost" onclick="window.print()" type="button">Print</button></div>`;
  return `<div class="panel-head"><h2>${esc(title)}</h2>${exportButtons}</div>
    <div class="table-wrap"><table><thead><tr><th>#</th><th>Event</th><th>Stacker</th><th>Division</th><th>Time</th><th>Gap</th></tr></thead><tbody>${body || `<tr><td colspan="6">No matching results yet</td></tr>`}</tbody></table></div>`;
}

function competitionGroupKey(row) {
  const specialMode = val("competitionSpecialMode") || "combined";
  const specialLabel = isCompetitionSpecialRow(row) ? "Special Stackers" : "Normal Stackers";
  const divisionMode = val("competitionDivision") || "all";
  if (divisionMode === "all-div" && specialMode === "separate") return `${row.division || "Blank"} - ${specialLabel}`;
  if (divisionMode === "all-div") return row.division || "Blank";
  if (specialMode === "separate") return specialLabel;
  return "Overall";
}

function isCompetitionSpecialRow(row) {
  return row.special === "Yes" || String(row.division || "").startsWith("SS ");
}

function competitionReportTitle(count) {
  const stageLabel = selectedOptionText("competitionStage");
  const typeLabel = selectedOptionText("competitionTypeReport");
  const divisionLabel = selectedOptionText("competitionDivision");
  const eventLabel = selectedOptionText("competitionEvent");
  return `${typeLabel} / ${stageLabel} / Division: ${divisionLabel} / Events: ${eventLabel} (${count})`;
}

function competitionResultTypeName(type) {
  if (type === "d") return "Doubles";
  if (type === "r") return "Timed Relay";
  return "Individual";
}

function competitionAllowedEvents(type, eventFilter) {
  if (eventFilter === "all-around") return ["all-around"];
  if (eventFilter === "all" || eventFilter === "all-min") {
    if (type === "i") return ["3-3-3", "3-6-3", "cycle"];
    if (type === "d") return ["cycle"];
    return ["3-6-3"];
  }
  return [normalizeEventName(eventFilter)];
}

function normalizeEventName(event) {
  return String(event || "").toLowerCase() === "cycle" ? "cycle" : String(event || "");
}

function competitionStageMatches(stage) {
  const selected = val("competitionStage") || "prelims";
  const normalized = String(stage || "").toLowerCase();
  if (selected === "all") return ["finals", "soc", "all"].includes(normalized);
  return normalized === selected;
}

function competitionAdvanceLimit() {
  const type = val("competitionTypeReport");
  if (type === "d") return Number(state.settings.advanceDoubles) || 0;
  if (type === "r") return Number(state.settings.advanceRelay) || 0;
  return Number(state.settings.advanceIndividuals) || 0;
}

function applyCompetitionReportPreset(key) {
  if (key === "export_json") return exportResults("json");
  if (key === "export_csv") return exportResults("csv");
  const preset = competitionReportPresets[key];
  if (!preset) return;
  setValue("competitionStage", preset.stage);
  setValue("competitionTypeReport", preset.type);
  setValue("competitionDivision", preset.division);
  setValue("competitionEvent", preset.event);
  setValue("competitionLimit", preset.limit);
  setValue("competitionGender", preset.gender);
  setValue("competitionSpecialMode", preset.specialMode || "combined");
  setValue("competitionGenderOp", "=");
  setValue("competitionCountry", "");
  setValue("competitionRegion", "");
  setValue("competitionOrg", "");
  setValue("competitionCountryOp", "=");
  setValue("competitionRegionOp", "=");
  setValue("competitionOrgOp", "=");
  setValue("competitionHighlight", preset.highlight);
  runCompetitionReport();
}

function currentCompetitionPreset() {
  return competitionReportPresets[val("competitionReportPreset")] || null;
}

function exportResults(format) {
  const rows = competitionReportRows();
  if (format === "json") {
    downloadText("NADITrack-results.json", JSON.stringify(rows, null, 2), "application/json");
    return;
  }
  const headers = ["Rank", "Event", "Name", "Division", "Time", "Gap", "Stage", "Country", "Region", "Org"];
  const csvRows = rows.map(row => [row.rank, row.event, row.name, row.division, fmt(row.time), row.gap ? fmt(row.gap) : "", row.stage, row.country, row.region, row.org]);
  downloadText("NADITrack-results.csv", [headers, ...csvRows].map(csvLine).join("\n"), "text/csv");
}

function csvLine(row) {
  return row.map(value => `"${String(value ?? "").replaceAll('"', '""')}"`).join(",");
}

function downloadText(filename, text, type) {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function selectedOptionText(id) {
  const select = document.getElementById(id);
  return select?.selectedOptions?.[0]?.textContent || "";
}

function runReport() {
  const out = document.getElementById("reportOutput");
  if (!out) return;
  try {
    const report = buildAdminReportData();
    out.innerHTML = adminReportHtml(report);
  } catch (error) {
    console.error("Unable to build admin report.", error);
    out.innerHTML = `<div class="report-empty"><strong>Unable to build report.</strong><br>${esc(error?.message || error)}</div>`;
  }
}

function buildAdminReportData() {
  const type = document.getElementById("reportType")?.value || "individuals";
  const group = document.getElementById("reportGroup")?.value || "";
  if (type === "division-counts") {
    const counts = Object.entries(groupCounts(filterReportRows(state.stackers), "division"))
      .sort(([a], [b]) => compareDivisionNames(a, b));
    return sortAdminReport({ title: "All Division Counts", headers: ["Division", "Stackers"], rows: counts, groups: [], meta: adminReportMeta(type, "") });
  }
  if (type === "results") {
    return sortAdminReport({
      title: "Competition Results",
      headers: ["Stage", "Name", "Event", "Official"],
      rows: bestResults().map(r => [r.stage, participantName(r.type, r.participant), r.event, fmt(official(r))]),
      groups: [],
      meta: adminReportMeta(type, "")
    });
  }
  const selectedColumns = selectedReportColumns(type);
  const sourceRows = filterReportRows(reportRowsForType(type));
  const title = reportTitle(type, group, sourceRows.length);
  const headers = selectedColumns.map(col => col.label);
  if (group) {
    const buckets = sourceRows.reduce((acc, item) => {
      const key = reportValue(item, group, type) || "Blank";
      if (!acc[key]) acc[key] = [];
      acc[key].push(selectedColumns.map(col => reportValue(item, col.key, type)));
      return acc;
    }, {});
    const groups = Object.entries(buckets)
      .sort(([a], [b]) => String(a).localeCompare(String(b), undefined, { numeric: true }))
      .map(([name, rows]) => ({ name, rows }));
    return sortAdminReport({ title, headers, rows: groups.flatMap(item => item.rows), groups, meta: adminReportMeta(type, group) });
  }
  const rows = sourceRows.map(item => selectedColumns.map(col => reportValue(item, col.key, type)));
  return sortAdminReport({ title, headers, rows, groups: [], meta: adminReportMeta(type, group) });
}

function sortAdminReport(report) {
  if (adminReportSort.index < 0 || adminReportSort.index >= report.headers.length) return report;
  const sortRows = rows => [...rows].sort((a, b) => compareReportCells(a[adminReportSort.index], b[adminReportSort.index]) * (adminReportSort.direction === "asc" ? 1 : -1));
  if (report.groups.length) {
    const groups = report.groups.map(group => ({ ...group, rows: sortRows(group.rows) }));
    return { ...report, groups, rows: groups.flatMap(group => group.rows) };
  }
  return { ...report, rows: sortRows(report.rows) };
}

function compareReportCells(a, b) {
  const aText = String(a ?? "").trim();
  const bText = String(b ?? "").trim();
  const numericCell = /^[-+]?(?:\d+(?:\.\d+)?|\.\d+)$/;
  if (numericCell.test(aText) && numericCell.test(bText)) return Number(aText) - Number(bText);
  return aText.localeCompare(bText, undefined, { numeric: true, sensitivity: "base" });
}

function sortAdminReportBy(index) {
  adminReportSort = {
    index,
    direction: adminReportSort.index === index && adminReportSort.direction === "asc" ? "desc" : "asc"
  };
  runReport();
}

function adminReportMeta(type, group) {
  return [
    ["Report Header", brandText("reportHeader")],
    ["Tournament", state.settings.name],
    ["Report Type", reportTypeLabel(type)],
    ["Group By", group ? reportLabel(group) : "None"],
    ["Country", filterSummary("reportCountry", "reportCountryOp")],
    ["Region", filterSummary("reportRegion", "reportRegionOp")],
    ["Org", filterSummary("reportOrg", "reportOrgOp")],
    ["Team", selectedOptionText("reportTeam") || "--"],
    ["Generated", stackMeetDateTime()]
  ];
}

function reportTypeLabel(type) {
  const labels = {
    individuals: "Individuals",
    doubles: "Doubles",
    "timed-relay": "Timed Relay",
    results: "Results",
    "division-counts": "Division Counts"
  };
  return labels[type] || type;
}

function filterSummary(valueId, opId) {
  const value = val(valueId);
  return value ? `${val(opId) || "="} ${value}` : "Any";
}

function adminReportHtml(report) {
  const rowsHtml = report.groups.length
    ? report.groups.map(group => `
      <tr class="group-row"><td colspan="${report.headers.length}">${esc(group.name)} (${group.rows.length})</td></tr>
      ${group.rows.map(row => reportRowHtml(row)).join("")}
    `).join("")
    : report.rows.map(row => reportRowHtml(row)).join("");
  return `
    <div class="report-document" data-report-kind="admin">
      <div class="report-actions no-print">
        <label class="print-layout-control">
          <span>Print Layout</span>
          <select id="adminPrintOrientation">
            <option value="portrait"${adminPrintOrientation === "portrait" ? " selected" : ""}>Portrait</option>
            <option value="landscape"${adminPrintOrientation === "landscape" ? " selected" : ""}>Landscape</option>
          </select>
        </label>
        <button class="ghost" data-action="print-admin-report" type="button">Print Report</button>
        <button class="ghost" data-action="export-admin-csv" type="button">Export CSV</button>
        <button class="ghost" data-action="export-admin-excel" type="button">Export Excel</button>
      </div>
      <header class="report-header">
        <p>${esc(brandText("reportHeader"))}</p>
        <h2>${esc(report.title)}</h2>
        <strong>${esc(state.settings.name)}</strong>
      </header>
      <div class="report-meta no-print">${report.meta.map(([label, value]) => `<div><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join("")}</div>
      <div class="table-wrap report-table-wrap">
        <table>
          <thead><tr>${report.headers.map((h, index) => `<th><button class="sort-header" data-action="sort-admin-report" data-sort-index="${index}" type="button">${esc(h)}${adminSortIndicator(index)}</button></th>`).join("")}</tr></thead>
          <tbody>${rowsHtml || `<tr><td colspan="${report.headers.length}">No matching records</td></tr>`}</tbody>
        </table>
      </div>
    </div>`;
}

function adminSortIndicator(index) {
  if (adminReportSort.index !== index) return "";
  return `<span>${adminReportSort.direction === "asc" ? "▲" : "▼"}</span>`;
}

function reportRowHtml(row) {
  return `<tr>${row.map(cell => `<td>${esc(cell)}</td>`).join("")}</tr>`;
}

function printAdminReport() {
  adminPrintOrientation = document.getElementById("adminPrintOrientation")?.value === "portrait" ? "portrait" : "landscape";
  const printPageStyle = document.createElement("style");
  printPageStyle.id = "adminReportPageStyle";
  printPageStyle.textContent = `@media print { @page { size: A4 ${adminPrintOrientation}; margin: 12mm; } }`;
  document.getElementById(printPageStyle.id)?.remove();
  document.head.appendChild(printPageStyle);

  const originalTitle = document.title;
  document.body.dataset.printTarget = "admin-report";
  document.title = "";
  const cleanup = () => {
    document.title = originalTitle;
    delete document.body.dataset.printTarget;
    printPageStyle.remove();
  };
  window.addEventListener("afterprint", cleanup, { once: true });
  window.print();
  setTimeout(cleanup, 1000);
}

function exportAdminReport(format) {
  const report = buildAdminReportData();
  if (format === "excel") {
    downloadText(`${slugify(report.title)}.xls`, excelReportHtml(report), "application/vnd.ms-excel");
    return;
  }
  const csvRows = [
    [brandText("reportHeader")],
    [state.settings.name],
    [report.title],
    [],
    report.headers,
    ...reportExportRows(report)
  ];
  downloadText(`${slugify(report.title)}.csv`, csvRows.map(csvLine).join("\n"), "text/csv");
}

function reportExportRows(report) {
  if (!report.groups.length) return report.rows;
  return report.groups.flatMap(group => [[group.name], ...group.rows]);
}

function excelReportHtml(report) {
  const bodyRows = report.groups.length
    ? report.groups.map(group => `<tr><td colspan="${report.headers.length}"><strong>${esc(group.name)} (${group.rows.length})</strong></td></tr>${group.rows.map(row => reportRowHtml(row)).join("")}`).join("")
    : report.rows.map(row => reportRowHtml(row)).join("");
  return `<!doctype html><html><head><meta charset="utf-8"><style>
    body{font-family:Arial,sans-serif} table{border-collapse:collapse;width:100%} th,td{border:1px solid #999;padding:6px;text-align:left} th{background:#e9eef2}
  </style></head><body>
    <h1>${esc(brandText("reportHeader"))}</h1><h2>${esc(state.settings.name)}</h2><h3>${esc(report.title)}</h3>
    <table><thead><tr>${report.headers.map(header => `<th>${esc(header)}</th>`).join("")}</tr></thead><tbody>${bodyRows}</tbody></table>
  </body></html>`;
}

function slugify(value) {
  return String(value || "report").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "report";
}

function reportTable(title, headers, rows) {
  return adminReportHtml({ title, headers, rows, groups: [], meta: adminReportMeta("", "") });
}

function groupedReportTable(title, columns, rows, group, type) {
  const headers = columns.map(col => col.label);
  const buckets = rows.reduce((acc, item) => {
    const key = reportValue(item, group, type) || "Blank";
    if (!acc[key]) acc[key] = [];
    acc[key].push(columns.map(col => reportValue(item, col.key, type)));
    return acc;
  }, {});
  const groups = Object.entries(buckets)
    .sort(([a], [b]) => String(a).localeCompare(String(b), undefined, { numeric: true }))
    .map(([name, groupRows]) => ({ name, rows: groupRows }));
  return adminReportHtml({ title, headers, rows: groups.flatMap(item => item.rows), groups, meta: adminReportMeta(type, group) });
}

function populateReportBuilder() {
  populateReportSelect("reportCountry", uniqueReportValues(state.stackers, "country"));
  populateReportSelect("reportRegion", uniqueReportValues(state.stackers, "region"));
  populateReportSelect("reportOrg", uniqueReportValues(state.stackers, "org"));
  populateReportColumns();
}

function populateReportSelect(id, values) {
  const select = document.getElementById(id);
  if (!select) return;
  const current = select.value;
  select.innerHTML = [`<option value="">Any</option>`, ...values.map(value => `<option value="${esc(value)}">${esc(value)}</option>`)].join("");
  if (values.includes(current)) select.value = current;
}

function populateReportColumns(preferredColumns = null) {
  const select = document.getElementById("reportColumns");
  const type = document.getElementById("reportType")?.value || "individuals";
  if (!select) return;
  const current = preferredColumns || Array.from(select.selectedOptions || []).map(option => option.value);
  const defaults = defaultReportColumns(type);
  const selected = current.length ? current : defaults;
  const options = reportColumns.filter(col => col.types.includes(type));
  select.innerHTML = options.map(col => `<option value="${esc(col.key)}" ${selected.includes(col.key) ? "selected" : ""}>${esc(col.label)}</option>`).join("");
}

function applyReportPreset(presetKey) {
  const preset = reportPresets[presetKey];
  if (!preset) return;
  setValue("reportType", preset.type);
  setValue("reportGroup", preset.group);
  setValue("reportTeam", preset.team);
  setValue("reportCountry", "");
  setValue("reportRegion", "");
  setValue("reportOrg", "");
  setValue("reportCountryOp", "=");
  setValue("reportRegionOp", "=");
  setValue("reportOrgOp", "=");
  populateReportColumns(preset.columns);
  runReport();
}

function uniqueReportValues(items, key) {
  return [...new Set(items.map(item => item[key]).filter(Boolean))]
    .sort((a, b) => String(a).localeCompare(String(b), undefined, { sensitivity: "base" }));
}

function defaultReportColumns(type) {
  if (type === "doubles" || type === "timed-relay") return ["id", "name", "country", "region", "division"];
  if (type === "division-counts") return ["division", "count"];
  return ["id", "name", "division", "country", "region", "gender", "email"];
}

function selectedReportColumns(type) {
  const selected = Array.from(document.getElementById("reportColumns")?.selectedOptions || []).map(option => option.value);
  const keys = selected.length ? selected : defaultReportColumns(type);
  return reportColumns.filter(col => keys.includes(col.key) && col.types.includes(type));
}

function reportRowsForType(type) {
  if (type === "doubles") return state.doubles.map(team => ({
    ...team,
    name: participantName("Doubles", team.id),
    region: team.region || teamRegion(team),
    country: teamCountry(team),
    division: doubleDivision(team),
    org: team.org || "",
    gender: "",
    email: ""
  }));
  if (type === "timed-relay") return (state.relays || []).map(team => ({
    ...team,
    name: participantName("Timed Relay", team.id),
    region: relayLocation(team),
    country: team.country || relayCountryForMembers(relayMemberIds(team)),
    division: relayDivision(team),
    org: team.org || "",
    gender: "",
    email: team.email || ""
  }));
  return state.stackers;
}

function filterReportRows(rows) {
  let filtered = [...rows];
  filtered = applyReportFilter(filtered, "country", val("reportCountry"), val("reportCountryOp"));
  filtered = applyReportFilter(filtered, "region", val("reportRegion"), val("reportRegionOp"));
  filtered = applyReportFilter(filtered, "org", val("reportOrg"), val("reportOrgOp"));
  const teamFilter = val("reportTeam");
  if (teamFilter === "not-doubles") filtered = filtered.filter(stacker => !doublesForStacker(stacker.id).length);
  if (teamFilter === "not-relay") filtered = filtered.filter(stacker => !relayForStacker(stacker.id));
  return filtered;
}

function applyReportFilter(rows, key, value, op) {
  if (!value) return rows;
  return rows.filter(row => (op === "!=") !== (String(row[key] || "") === value));
}

function reportValue(item, key, type) {
  if (key === "fname") return splitName(item.name).first;
  if (key === "lname") return splitName(item.name).last;
  if (key === "age") return item.age || ageOnCompetitionDate(item.dob, state.settings.start) || "";
  if (key === "doubles") return doublesForStacker(item.id).length ? "Yes" : "No";
  if (key === "doubles_partner" || key === "st_doubles_partner") return doublesForStacker(item.id).map(team => doublePartnerNameForStacker(team, item.id)).join(", ");
  if (key === "d_id") return doublesForStacker(item.id).map(team => team.id).join(", ");
  if (key === "doubles_cp_partner") return doublesForStacker(item.id).filter(team => team.type === "child_parent").map(team => doublePartnerNameForStacker(team, item.id)).join(", ");
  if (key === "relay") return relayForStacker(item.id) ? "Yes" : "No";
  if (key === "relay_team") return relayForStacker(item.id)?.id || "";
  if (key === "avg_time") return item.avgTime || item.avg_time || item.avg363 || item["avg_3-6-3"] || "";
  if (key === "amt") return item.amt || item.amount || item.fee || "";
  if (type === "doubles" && key === "name") return item.name || participantName("Doubles", item.id);
  if (type === "timed-relay" && key === "name") return item.name || participantName("Timed Relay", item.id);
  return item[key] ?? "";
}

function splitName(name) {
  const parts = String(name || "").trim().split(/\s+/).filter(Boolean);
  return { first: parts[0] || "", last: parts.slice(1).join(" ") };
}

function doublesForStacker(stackerId) {
  return state.doubles.filter(team => registeredDoubleMemberIds(team).includes(stackerId));
}

function registeredDoubleMemberIds(team) {
  return [team.one, team.two, team.childStackerId, team.parentStackerId].filter(Boolean);
}

function printableDoublesTeams() {
  const builtTeams = completedDoubles();
  if (builtTeams.length) return builtTeams;
  return registeredDoublesFromStackers();
}

function findDoublesTeam(id) {
  return state.doubles.find(team => team.id === id) || registeredDoublesFromStackers().find(team => team.id === id) || null;
}

function registeredDoublesFromStackers() {
  const teams = new Map();
  state.stackers.forEach(stacker => {
    const partnerId = registrationField(stacker, ["d_id", "doublesPartnerId", "doubles_partner_id", "partnerId"]);
    const partnerName = registrationField(stacker, ["doubles_partner", "st_doubles_partner", "doublesPartner", "partnerName"]);
    const teamId = registrationField(stacker, ["doubles_team", "doublesTeam", "doublesTeamId", "d_id"]) || (partnerId ? `REG-${[stacker.id, partnerId].sort().join("-")}` : `REG-${stacker.id}`);
    if (!partnerId && !partnerName) return;
    if (teams.has(teamId)) return;
    const partner = partnerId ? state.stackers.find(item => item.id === partnerId) : null;
    teams.set(teamId, {
      id: teamId,
      type: "normal",
      status: "complete",
      one: stacker.id,
      two: partner?.id || "",
      parentName: partner ? "" : partnerName,
      division: stacker.division || stacker.customDivision || "Open",
      country: stacker.country || partner?.country || "Malaysia",
      region: stacker.region || partner?.region || ""
    });
  });
  return [...teams.values()];
}

function registrationField(stacker, keys) {
  const key = keys.find(candidate => String(stacker?.[candidate] || "").trim());
  return key ? String(stacker[key]).trim() : "";
}

function teamRegion(team) {
  const members = registeredDoubleMemberIds(team).map(id => state.stackers.find(stacker => stacker.id === id)).filter(Boolean);
  return team.region || members.find(member => member.region)?.region || "";
}

function reportTitle(type, group, count) {
  const names = {
    individuals: "Individuals",
    doubles: "Doubles Teams",
    "timed-relay": "Timed Relay Teams",
    results: "Competition Results",
    "division-counts": "All Division Counts"
  };
  return `${names[type] || "Report"}${group ? ` grouped by ${reportLabel(group)}` : ""} (${count})`;
}

function reportLabel(key) {
  return reportColumns.find(col => col.key === key)?.label || key;
}

function handleReportTypeChange() {
  populateReportColumns();
  runReport();
}

document.addEventListener("click", async (event) => {
  const target = event.target.closest("[data-route], [data-action]");
  if (!target) return;
  if (target.dataset.route) {
    if (target.dataset.reportTab) reportTab = ["finals", "admin"].includes(target.dataset.reportTab) ? target.dataset.reportTab : reportTab;
    route = target.dataset.route;
    render();
    return;
  }
  const action = target.dataset.action;
  let shouldRender = true;
  let shouldSave = true;
  if (action === "mark-read") state.notifications = state.notifications.map(n => ({ ...n, read: true }));
  if (action === "save-settings") await saveSettings();
  if (action === "save-language") saveLanguage();
  if (action === "save-events") saveEvents();
  if (action === "save-leaderboard") saveLeaderboard();
  if (action === "open-leaderboard") { await openLeaderboardDisplay(); shouldRender = false; shouldSave = false; }
  if (action === "save-divisions") saveDivisions();
  if (action === "add-division") addDivision();
  if (action === "remove-division") removeDivision(target.dataset.division);
  if (action === "create-sql-competition") await createSqlCompetition();
  if (action === "refresh-stackers") { await refreshSqlStackers({ rerender: true }); shouldRender = false; }
  if (action === "show-stacker-form") { showStackerForm(); shouldRender = false; }
  if (action === "add-stacker") await addStacker();
  if (action === "sort-stackers") { sortStackerTable(target.dataset.sortKey); shouldRender = false; }
  if (action === "print-stacker-sheet") { printSingleStackerSheet(target.dataset.id); shouldRender = false; }
  if (action === "edit-stacker") { loadStackerForEdit(target.dataset.id); shouldRender = false; }
  if (action === "cancel-stacker-edit") { clearStackerForm(); shouldRender = false; }
  if (action === "delete-stacker") { requestDeleteStacker(target.dataset.id); shouldRender = false; }
  if (action === "cancel-delete-stacker") { closeDeleteStackerConfirmation(); shouldRender = false; }
  if (action === "confirm-delete-stacker") await deleteStacker(pendingDeleteStackerId);
  if (action === "edit-stacker-double") { stackerDoubleEditorOpen = true; updateStackerDoublesInfo(); shouldRender = false; }
  if (action === "save-stacker-double") { saveStackerDoubleAssignment(); shouldRender = false; }
  if (action === "add-double") addDouble();
  if (action === "edit-double") { loadDoubleForEdit(target.dataset.id); shouldRender = false; }
  if (action === "cancel-double-edit") { clearDoubleForm(); shouldRender = false; }
  if (action === "delete-double") deleteDouble(target.dataset.id);
  if (action === "switch-doubles-tab") { doublesTab = target.dataset.doublesTab || "completed"; shouldRender = false; renderDoubles(); }
  if (action === "add-relay") addRelay();
  if (action === "edit-relay") { loadRelayForEdit(target.dataset.id); shouldRender = false; }
  if (action === "cancel-relay-edit") { clearRelayForm(); shouldRender = false; }
  if (action === "delete-relay") deleteRelay(target.dataset.id);
  if (action === "switch-relay-tab") { relayTab = target.dataset.relayTab || "ready"; shouldRender = false; renderRelay(); }
  if (action === "save-awards") {
    const beforeAwards = auditSnapshot(state.awards);
    saveAwards();
    appendCompetitionAuditLog({
      action: "awards.updated",
      entityType: "Awards",
      summary: "Award planner settings updated.",
      before: beforeAwards,
      after: state.awards
    });
    shouldRender = false;
  }
  if (action === "export-awards-csv") { exportAwardsCsv(); shouldRender = false; }
  if (action === "export-competition-audit-csv") { exportCompetitionAuditCsv(); shouldRender = false; shouldSave = false; }
  if (action === "paperwork") { buildPaperwork(target.dataset.type); shouldRender = false; }
  if (action === "print-paper-preview") { printPaperPreview(); shouldRender = false; }
  if (action === "build-bracket") { buildBracket(); shouldRender = false; }
  if (action === "lookup-prelim-participant") { loadPrelimParticipant(); shouldRender = false; }
  if (action === "load-missing-prelim") { loadMissingPrelim(target.dataset.id); shouldRender = false; }
  if (action === "clear-prelim-entry") { clearPrelimEntry(); shouldRender = false; }
  if (action === "save-prelim-results") { await savePrelimResults(); shouldRender = false; shouldSave = false; }
  if (action === "lookup-final-sheet") { loadFinalSheetFromInput(); shouldRender = false; }
  if (action === "load-final-missing") { loadFinalSheet(target.dataset.id); shouldRender = false; }
  if (action === "clear-final-sheet") { clearFinalSheet(); shouldRender = false; }
  if (action === "save-final-results") { saveFinalResults(); shouldRender = false; }
  if (action === "print-final-sheet") { printCurrentFinalSheet(); shouldRender = false; }
  if (action === "save-result") saveResult();
  if (action === "delete-result") {
    const result = state.results.find(item => item.id === target.dataset.id);
    state.results = state.results.filter(r => r.id !== target.dataset.id);
    if (result) appendCompetitionAuditLog({
      action: "results.deleted",
      entityType: "Result",
      entityId: result.id,
      summary: `${result.stage} ${result.event} result deleted for ${result.participant}.`,
      before: result,
      after: null
    });
  }
  if (action === "switch-report-tab") { switchReportTab(target.dataset.reportTab); shouldRender = false; shouldSave = false; }
  if (action === "run-finals-report") { runFinalsReport(); shouldRender = false; shouldSave = false; }
  if (action === "generate-qualification-snapshots") { generateQualificationSnapshots(); shouldRender = false; }
  if (action === "approve-qualification-snapshot") { approveQualificationSnapshot(target.dataset.id); shouldRender = false; }
  if (action === "export-finals-csv") { exportFinalsCsv(); shouldRender = false; shouldSave = false; }
  if (action === "print-finals-report") { printFinalsReport(); shouldRender = false; shouldSave = false; }
  if (action === "run-competition-report") { runCompetitionReport(); shouldRender = false; shouldSave = false; }
  if (action === "export-results-json") { exportResults("json"); shouldRender = false; shouldSave = false; }
  if (action === "export-results-csv") { exportResults("csv"); shouldRender = false; shouldSave = false; }
  if (action === "print-admin-report") { printAdminReport(); shouldRender = false; shouldSave = false; }
  if (action === "export-admin-csv") { exportAdminReport("csv"); shouldRender = false; shouldSave = false; }
  if (action === "export-admin-excel") { exportAdminReport("excel"); shouldRender = false; shouldSave = false; }
  if (action === "sort-admin-report") { sortAdminReportBy(Number(target.dataset.sortIndex)); shouldRender = false; shouldSave = false; }
  if (action === "run-report") { runReport(); shouldRender = false; shouldSave = false; }
  if (shouldRender) render();
  if (shouldSave) {
    try {
      await saveState();
    } catch (error) {
      console.error("Unable to save competition state to the NADITrack API.", error);
    }
  }
});

document.addEventListener("change", (event) => {
  if (route === "doubles" && ["doubleType", "doubleStatus"].includes(event.target.id)) {
    updateDoubleFormMode();
    return;
  }
  if (route !== "reports") return;
  if (event.target.id === "adminPrintOrientation") {
    adminPrintOrientation = event.target.value === "portrait" ? "portrait" : "landscape";
    return;
  }
  if (event.target.id === "competitionReportPreset") {
    applyCompetitionReportPreset(event.target.value);
    return;
  }
  if (event.target.closest(".report-filters") && event.target.id?.startsWith("competition")) {
    runCompetitionReport();
    return;
  }
  if (event.target.closest(".report-filters") && event.target.id?.startsWith("finals")) {
    runFinalsReport();
    return;
  }
  if (event.target.id === "commonReport") {
    applyReportPreset(event.target.value);
    return;
  }
  if (event.target.id === "reportType") {
    handleReportTypeChange();
    return;
  }
  if (event.target.closest(".report-filters")) runReport();
});

window.addEventListener("hashchange", () => {
  route = location.hash.replace("#", "") || "dashboard";
  render();
});

document.getElementById("exportXmlBtn")?.addEventListener("click", async () => {
  if (selectedSqlCompetitionId) await refreshSqlStackers({ allowEditing: true, rerender: false });
  const blob = new Blob([stateToXml(state)], { type: "application/xml" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = "NADITrack-data.xml";
  link.click();
  URL.revokeObjectURL(link.href);
});

document.getElementById("importXmlInput")?.addEventListener("change", async (event) => {
  const file = event.target.files?.[0];
  if (!file) return;
  try {
    state = xmlToState(await file.text());
    await importSqlStackersFromState(state);
    render();
  } catch (error) {
    alert("This XML file could not be imported. Please check that it came from this app.");
    return;
  } finally {
    event.target.value = "";
  }
  try {
    await saveState();
  } catch (error) {
    console.error("Unable to save competition state to the NADITrack API.", error);
  }
});

async function importSqlStackersFromState(importedState) {
  if (!selectedSqlCompetitionId) return;
  const importedStackers = importedState.stackers || [];
  const existingRecords = await stackerApi.list(selectedSqlCompetitionId);
  const existingByCode = new Map(existingRecords.map(record => [String(record.stackerCode || ""), record]));
  for (const stacker of importedStackers) {
    if (!stacker.id) continue;
    const existing = existingByCode.get(String(stacker.id));
    if (existing) await stackerApi.update(selectedSqlCompetitionId, existing.id, runtimeStackerToSql(stacker));
    else await stackerApi.create(selectedSqlCompetitionId, runtimeStackerToSql(stacker));
  }
  await refreshSqlStackers({ allowEditing: true, rerender: false });
}

document.getElementById("resetBtn")?.addEventListener("click", async () => {
  state = normalizeState(structuredClone(demo));
  render();
  try {
    await saveState();
  } catch (error) {
    console.error("Unable to save competition state to the NADITrack API.", error);
  }
});

async function saveSettings() {
  const before = auditSnapshot(state.settings);
  const selectedAgeMode = val("settingAgeCalculation") === "yearBorn" ? "yearBorn" : "actual";
  if (selectedSqlCompetitionId) await saveCompetitionAgeCalculation(selectedAgeMode);
  ageCalculationMode = selectedAgeMode;
  state.settings = {
    name: val("settingName"),
    type: val("settingType"),
    start: val("settingStart"),
    end: val("settingEnd"),
    kbsLogo: val("settingKbsLogo"),
    prelims: normalizePrelimRounds(val("settingPrelims")),
    finals: val("settingFinals"),
    soc: val("settingSoc"),
    prelimTimes: val("settingPrelimTimes"),
    paperless: val("settingPaperless"),
    language: val("settingLanguage") || state.settings.language || "en",
    ageCalculationMode,
    separateSpecialDivisionsByGender: document.getElementById("settingSeparateSpecialGender")?.checked === true,
    advanceIndividuals: Number(val("settingAdvanceIndividuals")),
    advanceDoubles: Number(val("settingAdvanceDoubles")),
    advanceCpDoubles: Number(val("settingAdvanceCpDoubles")),
    advanceRelay: Number(val("settingAdvanceRelay")),
    timeSheetInput: val("settingTimeSheetInput")
  };
  applyCompetitionAgeCalculation(selectedAgeMode);
  refreshDivisionCountBadges(divisionCountSummary(state.divisionSettings));
  appendCompetitionAuditLog({
    action: "settings.updated",
    entityType: "Settings",
    summary: "Competition settings updated.",
    before,
    after: state.settings
  });
}

function saveLanguage() {
  state.settings.language = val("languageActive") || "en";
  document.querySelectorAll("[data-language-key]").forEach(input => {
    const code = input.dataset.languageCode || "ms";
    state.translations[code] = state.translations[code] || {};
    state.translations[code][input.dataset.languageKey] = input.value.trim() || input.dataset.languageKey;
  });
}

function saveEvents() {
  const before = auditSnapshot(state.events);
  state.events = {};
  document.querySelectorAll("[data-event-group]").forEach(input => {
    if (!state.events[input.dataset.eventGroup]) state.events[input.dataset.eventGroup] = [];
    if (input.checked) state.events[input.dataset.eventGroup].push(input.value);
  });
  appendCompetitionAuditLog({
    action: "events.updated",
    entityType: "Events",
    summary: "Competition event setup updated.",
    before,
    after: state.events
  });
}

function saveDivisions() {
  const before = auditSnapshot({ divisionSettings: state.divisionSettings, divisions: state.divisions });
  updateDivisionSettingsFromForm({ recalculateEntries: true });
  appendCompetitionAuditLog({
    action: "divisions.updated",
    entityType: "Divisions",
    summary: "Competition division setup updated.",
    before,
    after: { divisionSettings: state.divisionSettings, divisions: state.divisions }
  });
}

function updateDivisionSettingsFromForm({ recalculateEntries = false } = {}) {
  state.divisionSettings = readDivisionSettingsFromForm();
  state.divisions = appendStandardImportedDivisions(generateDivisionNames(state.divisionSettings), state.stackers);
  if (!recalculateEntries) return;
  state.stackers = recalculateStackerDivisions(state.stackers, state.divisionSettings, state.settings.start, state.settings.separateSpecialDivisionsByGender === true);
  state.doubles = state.doubles.map(team => ({
    ...team,
    division: generatedDoublesDivision(team.type || "normal", team.one, team.two)
  }));
  state.relays = state.relays.map(team => ({
    ...team,
    timedRelayDivision: generatedRelayDivision(relayMemberIds(team), "timedRelay"),
    headToHeadDivision: generatedRelayDivision(relayMemberIds(team), "headToHeadRelay")
  }));
}

function readDivisionSettingsFromForm() {
  const settings = {
    combined: [],
    male: [],
    female: [],
    special: [],
    doubles: [],
    childParentDoubles: [],
    specialDoubles: [],
    specialChildParentDoubles: [],
    timedRelay: [],
    headToHeadRelay: [],
    custom: [...(state.divisionSettings.custom || [])]
  };
  document.querySelectorAll("[data-division-group]").forEach(input => {
    if (input.checked) settings[input.dataset.divisionGroup].push(Number(input.value));
  });
  Object.keys(settings).forEach(key => {
    if (Array.isArray(settings[key])) settings[key] = [...new Set(settings[key])].sort((a, b) => a - b);
  });
  return settings;
}

function generateDivisionNames(settings) {
  const generated = [
    ...divisionRanges(divisionPath(settings, "male", "Male")).flat(),
    ...divisionRanges(divisionPath(settings, "female", "Female")).flat(),
    ...divisionRanges(settings.special || [], "Special"),
    ...teamDivisionRanges(settings.doubles || [], ""),
    ...teamDivisionRanges(settings.childParentDoubles || [], "Child/Parent "),
    ...teamDivisionRanges(settings.specialDoubles || [], "SS "),
    ...teamDivisionRanges(settings.specialChildParentDoubles || [], "SS Child/Parent "),
    ...(settings.custom || [])
  ];
  return sortedDivisions(dedupeDivisions(generated));
}

function sortedDivisions(divisions) {
  return [...new Set((divisions || []).filter(Boolean))]
    .sort((a, b) => compareDivisionNames(a, b));
}

function compareDivisionNames(a, b) {
  const left = divisionSortInfo(a);
  const right = divisionSortInfo(b);
  return left.start - right.start
    || left.end - right.end
    || left.group - right.group
    || String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
}

function divisionSortInfo(name) {
  const text = String(name || "").trim();
  let match = /^(\d+)\s*&\s*Under\s+(Combined|Male|Female)$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: divisionGroupSort(match[2]) };
  match = /^(\d+)U\s+C$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: divisionGroupSort("Combined") };
  match = /^(\d+)U$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: 5 };
  match = /^(\d+)\+$/i.exec(text);
  if (match) return { start: Number(match[1]), end: 1000, group: 5 };
  match = /^(\d+)-(\d+)\s+(Combined|Male|Female)$/i.exec(text);
  if (match) return { start: Number(match[1]), end: Number(match[2]), group: divisionGroupSort(match[3]) };
  match = /^(\d+)\s+(Combined|Male|Female)$/i.exec(text);
  if (match) return { start: Number(match[1]), end: Number(match[1]), group: divisionGroupSort(match[2]) };
  match = /^SS\s+(\d+)\s*&\s*Under\s+L\d+$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: 4 };
  match = /^SS\s+(\d+)-(\d+)\s+L\d+$/i.exec(text);
  if (match) return { start: Number(match[1]), end: Number(match[2]), group: 4 };
  match = /^SS\s+(\d+)\s+L\d+$/i.exec(text);
  if (match) return { start: Number(match[1]), end: Number(match[1]), group: 4 };
  match = /^SS\s+(\d+)U$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: 6 };
  if (/^SS\s+Child\/Parent$/i.test(text)) return { start: 0, end: 1000, group: 7 };
  match = /^Child\/Parent\s+(\d+)U$/i.exec(text);
  if (match) return { start: 0, end: Number(match[1]), group: 8 };
  match = /^Child\/Parent\s+(\d+)\+$/i.exec(text);
  if (match) return { start: Number(match[1]), end: 1000, group: 8 };
  if (/^SS\s+Relay$/i.test(text)) return { start: 0, end: 1000, group: 7 };
  if (/^Open$/i.test(text)) return { start: 19, end: 1000, group: 5 };
  if (/^Collegiate C$/i.test(text)) return { start: 19, end: 24, group: 0 };
  match = /^Masters\s+(\d)(?:-(\d))?\s+C$/i.exec(text);
  if (match) {
    const startLevel = Number(match[1]);
    const endLevel = Number(match[2] || match[1]);
    return { start: masterStartAge(startLevel), end: masterEndAge(endLevel), group: 0 };
  }
  return { start: 1000, end: 1000, group: 9 };
}

function divisionGroupSort(label) {
  return { combined: 0, male: 1, female: 2 }[String(label || "").toLowerCase()] ?? 8;
}

function masterStartAge(level) {
  return { 1: 25, 2: 35, 3: 45, 4: 60 }[level] || 1000;
}

function masterEndAge(level) {
  return { 1: 34, 2: 44, 3: 59, 4: 100 }[level] || 1000;
}

function divisionRanges(cutoffsOrPath, fallbackLabel = "") {
  if (fallbackLabel === "Special") {
    let previous = 0;
    return [...cutoffsOrPath].sort((a, b) => a - b).map(cutoff => {
      const start = previous + 1;
      previous = cutoff;
      if (start <= 4) return `SS ${cutoff} & Under L1`;
      if (start === cutoff) return `SS ${cutoff} L1`;
      return `SS ${start}-${cutoff} L1`;
    });
  }
  const path = cutoffsOrPath
    .map(item => typeof item === "number" ? { age: item, label: fallbackLabel } : item)
    .sort((a, b) => a.age - b.age);
  let previous = 0;
  return path.map(({ age: cutoff, label }) => {
    const start = previous + 1;
    previous = cutoff;
    if (label === "Combined") {
      const standardNames = standardCombinedDivisionNames(start, cutoff);
      if (standardNames.length) return standardNames;
    }
    if (start <= 4) return `${cutoff} & Under ${label}`;
    if (start === cutoff) return `${cutoff} ${label}`;
    return `${start}-${cutoff} ${label}`;
  });
}

function dedupeDivisions(divisions) {
  const mapped = divisions.flat().filter(Boolean);
  const officialAdultNames = new Set(mapped.filter(isOfficialAdultDivision));
  return [...new Set(mapped.filter(division => {
    const official = officialNameForCombinedRange(division);
    return !official || !officialAdultNames.has(official);
  }))];
}

function officialNameForCombinedRange(division) {
  const range = /^(\d+)-(\d+)\s+Combined$/i.exec(division);
  if (!range) return "";
  const names = standardCombinedDivisionNames(Number(range[1]), Number(range[2]));
  return names.length === 1 ? names[0] : "";
}

function isOfficialAdultDivision(division) {
  return /^Collegiate C$/i.test(division) || /^Masters \d(?:-\d)? C$/i.test(division);
}

function standardCombinedDivisionName(start, cutoff) {
  const names = standardCombinedDivisionNames(start, cutoff);
  return names.length === 1 ? names[0] : "";
}

function standardCombinedDivisionNames(start, cutoff) {
  const names = [];
  if (start <= 24 && cutoff >= 19) names.push("Collegiate C");
  if (cutoff >= 25) {
    const firstMaster = masterLevelForAge(Math.max(start, 25));
    const lastMaster = masterLevelForAge(cutoff);
    if (firstMaster && lastMaster) {
      names.push(firstMaster === lastMaster ? `Masters ${firstMaster} C` : `Masters ${firstMaster}-${lastMaster} C`);
    }
  }
  return names;
}

function masterLevelForAge(age) {
  if (age >= 25 && age <= 34) return 1;
  if (age >= 35 && age <= 44) return 2;
  if (age >= 45 && age <= 59) return 3;
  if (age >= 60) return 4;
  return 0;
}

function saveLeaderboard() {
  state.leaderboard = {
    type: val("leaderType"),
    stage: val("leaderStage"),
    fontSize: clampNumber(numericFromSetting(val("leaderFontSize")), 1, 0.5, 2),
    bg: val("leaderBg"),
    color: val("leaderColor"),
    progressHeight: clampNumber(numericFromSetting(val("leaderProgressHeight")), 4, 1, 20),
    pause: clampNumber(numericFromSetting(val("leaderPause")), 8, 1, 300),
    limit: clampNumber(numericFromSetting(val("leaderLimit")), 10, 3, 50)
  };
}

async function openLeaderboardDisplay() {
  saveLeaderboard();
  await saveState();
  const url = `${location.origin}${location.pathname}${location.search}#leaderboard`;
  const display = window.open(url, "stackmeetLeaderboard", "popup=yes,width=1280,height=720,menubar=no,toolbar=no,location=no,status=no,scrollbars=no,resizable=yes");
  if (!display) alert("Please allow popups for NADITrack, then click Open Display again.");
}

function addDivision() {
  const name = prompt("Division name");
  if (!name) return;
  const cleanName = name.trim();
  if (!cleanName) return;
  if (!state.divisionSettings.custom.includes(cleanName)) state.divisionSettings.custom.push(cleanName);
  if (!state.divisions.includes(cleanName)) state.divisions.push(cleanName);
  state.divisions = sortedDivisions(state.divisions);
}

function removeDivision(name) {
  if (!name) return;
  state.divisionSettings.custom = (state.divisionSettings.custom || []).filter(division => division !== name);
  state.divisions = sortedDivisions(state.divisions.filter(division => division !== name));
}

async function importStackersCsvFile(event) {
  const file = event.target.files?.[0];
  if (!file) return;
  try {
    const rows = parseCsv(await file.text());
    const stackers = rows.map(mapStackTrackCsvRow).filter(Boolean);
    if (!stackers.length) throw new Error("No stackers found");
    if (!selectedSqlCompetitionId) throw new Error("Create or select a SQL competition first");
    const confirmed = confirm(`Import ${stackers.length} stackers from ${file.name}? Existing SQL-native stackers will not be replaced.`);
    if (!confirmed) return;
    setSaveStatus("Saving...", "saving");
    let imported = 0;
    let skipped = 0;
    let failed = 0;
    for (const stacker of stackers) {
      try {
        await stackerApi.create(selectedSqlCompetitionId, runtimeStackerToSql(stacker));
        imported += 1;
      } catch (error) {
        if (error.status === 409) skipped += 1;
        else failed += 1;
      }
    }
    await refreshSqlStackers({ allowEditing: true, rerender: false });
    flashMessage = { type: failed ? "error" : "success", text: `${file.name}: ${imported} imported, ${skipped} skipped, ${failed} failed.` };
    setSaveStatus(failed ? "Save Failed" : "Saved", failed ? "failed" : "saved");
    render();
  } catch (error) {
    flashMessage = { type: "error", text: "This CSV could not be imported. Please check that it is a supported Individuals report and a competition is selected." };
    setSaveStatus("Save Failed", "failed");
    render();
  } finally {
    event.target.value = "";
  }
}

function appendStandardImportedDivisions(divisions, stackers) {
  return sortedDivisions([
    ...(divisions || []),
    ...stackers.map(stacker => stacker.standardDivision).filter(Boolean)
  ]);
}

function mapStackTrackCsvRow(row) {
  const id = cleanCsvValue(row._ID || row.ID);
  const name = cleanCsvValue(row.Name) || [row["First Name"], row["Last Name"]].map(cleanCsvValue).filter(Boolean).join(" ");
  if (!id || !name) return null;
    const sourceDivision = cleanCsvValue(row.Division);
    const customDivision = isCustomStackTrackDivision(sourceDivision) ? sourceDivision : "";
  const standardDivision = isStandardImportedDivision(sourceDivision) ? sourceDivision : "";
  const dob = normalizedDateValue(row.DOB) || cleanCsvValue(row.DOB);
  const stacker = {
    id,
    name,
    gender: cleanCsvValue(row.Gender).toUpperCase() === "F" ? "F" : "M",
    dob,
    age: ageOnCompetitionDate(dob, state.settings.start) || "",
    division: customDivision || "",
    customDivision,
    standardDivision,
    special: isSpecialStackTrackDivision(sourceDivision) ? "Yes" : "No",
    org: cleanCsvValue(row.Org) || "Independent",
    country: cleanCsvValue(row.Country) || "Malaysia",
    region: cleanCsvValue(row.Region).replace(/^--$/, ""),
    email: cleanCsvValue(row.Email),
    phone: cleanCsvValue(row.Phone),
    paid: "Yes",
    checkedIn: "No"
  };
  stacker.division = divisionForStacker(stacker, state.divisionSettings, state.settings.start, state.settings.separateSpecialDivisionsByGender === true) || stacker.division || sourceDivision || "Open";
  return stacker;
}

function isSpecialStackTrackDivision(division) {
  return /^SS\b/i.test(division) || /down syndrome|special|disab/i.test(division);
}

function isCustomStackTrackDivision(division) {
  if (!division) return false;
  if (isStandardImportedDivision(division)) return false;
  if (/^SS\b/i.test(division)) return false;
  if (/^\d+U\s+C$/i.test(division)) return false;
  if (/^\d+(-\d+)?\s+[MFC]$/i.test(division)) return false;
  return true;
}

function isStandardImportedDivision(division) {
  return /^Masters\s+\d+-\d+\s+C$/i.test(division) || /^Collegiate\s+C$/i.test(division);
}

function cleanCsvValue(value) {
  return String(value ?? "").trim();
}

function parseCsv(text) {
  const rows = [];
  let row = [];
  let cell = "";
  let quoted = false;
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];
    const next = text[index + 1];
    if (quoted && char === "\"" && next === "\"") {
      cell += "\"";
      index += 1;
    } else if (char === "\"") {
      quoted = !quoted;
    } else if (char === "," && !quoted) {
      row.push(cell);
      cell = "";
    } else if ((char === "\n" || char === "\r") && !quoted) {
      if (char === "\r" && next === "\n") index += 1;
      row.push(cell);
      if (row.some(value => value.trim())) rows.push(row);
      row = [];
      cell = "";
    } else {
      cell += char;
    }
  }
  row.push(cell);
  if (row.some(value => value.trim())) rows.push(row);
  const headers = rows.shift()?.map(header => header.replace(/^\uFEFF/, "").trim()) || [];
  return rows.map(values => Object.fromEntries(headers.map((header, index) => [header, values[index] ?? ""])));
}

async function addStacker() {
  const name = val("stName").trim();
  if (!name) {
    flashMessage = { type: "error", text: "Please enter the stacker name first." };
    return;
  }
  if (!val("stDob")) {
    flashMessage = { type: "error", text: "Please enter the date of birth so the division can be generated." };
    return;
  }

  const id = editingStackerId || nextStackerCode();
  if (!editingStackerId && state.stackers.some(s => s.id === id)) {
    flashMessage = { type: "error", text: `Stacker ID ${id} already exists. Please try again.` };
    return;
  }

  const stacker = {
    id,
    name,
    gender: val("stGender"),
    dob: normalizedDateValue(val("stDob")) || val("stDob"),
    age: ageOnCompetitionDate(val("stDob"), state.settings.start) || "",
    division: stackerDivisionFromForm(),
    customDivision: val("stCustomDivision").trim(),
    special: isSpecialStacker() ? "Yes" : "No",
    org: val("stOrg").trim() || "Independent",
    country: val("stCountry").trim() || "Malaysia",
    region: val("stRegion").trim(),
    email: val("stEmail").trim(),
    phone: val("stPhone").trim(),
    paid: val("stPaid"),
    checkedIn: val("stCheckedIn")
  };
  if (!selectedSqlCompetitionId) {
    flashMessage = { type: "error", text: "Create or select a SQL competition before saving a stacker." };
    return;
  }
  setSaveStatus("Saving...", "saving");
  try {
    const existing = editingStackerId ? state.stackers.find(item => item.id === editingStackerId) : null;
    if (existing) await stackerApi.update(selectedSqlCompetitionId, existing.sqlId, runtimeStackerToSql(stacker));
    else await stackerApi.create(selectedSqlCompetitionId, runtimeStackerToSql(stacker));
    await refreshSqlStackers({ allowEditing: true, rerender: false });
    const savedStacker = state.stackers.find(item => item.id === stacker.id) || stacker;
    appendCompetitionAuditLog({
      action: existing ? "stacker.updated" : "stacker.created",
      entityType: "Stacker",
      entityId: stacker.id,
      summary: `${stacker.id} ${stacker.name} ${existing ? "updated" : "created"}.`,
      before: existing,
      after: savedStacker
    });
    await persistCompetitionAuditLog();
    flashMessage = { type: "success", text: existing ? `${stacker.name} was updated.` : `${stacker.name} was added as stacker ${stacker.id}.` };
    editingStackerId = "";
    stackerFormVisible = false;
    focusStackerListAfterRender = true;
    setSaveStatus("Saved", "saved");
  } catch (error) {
    flashMessage = { type: "error", text: `Save Failed: ${error.message}` };
    setSaveStatus("Save Failed", "failed");
  }
}

function loadStackerForEdit(id) {
  const stacker = state.stackers.find(item => item.id === id);
  if (!stacker) return;
  editingStackerId = id;
  stackerFormVisible = true;
  const form = document.getElementById("stackerForm");
  if (form) form.hidden = false;
  const showFormButton = document.getElementById("showStackerFormBtn");
  if (showFormButton) showFormButton.hidden = true;
  stackerDoubleEditorOpen = false;
  setValue("stName", stacker.name);
  setValue("stGender", stacker.gender || "M");
  setValue("stDob", normalizedDateValue(stacker.dob) || stacker.dob || "");
  setValue("stAge", ageOnCompetitionDate(stacker.dob, state.settings.start) || stacker.age || "");
  const special = document.getElementById("stSpecial");
  if (special) special.checked = stacker.special === "Yes";
  setValue("stCustomDivision", stacker.customDivision || "");
  setValue("stOrg", stacker.org || "");
  setValue("stCountry", stacker.country || "Malaysia");
  setValue("stRegion", stacker.region || "");
  setValue("stEmail", stacker.email || "");
  setValue("stPhone", stacker.phone || "");
  setValue("stPaid", stacker.paid || "No");
  setValue("stCheckedIn", stacker.checkedIn || "No");
  updateStackerDivisionPreview();
  syncStackerEditState();
  document.getElementById("stName")?.focus();
}

function showStackerForm() {
  clearStackerForm(false);
  stackerFormVisible = true;
  const form = document.getElementById("stackerForm");
  if (form) form.hidden = false;
  const showFormButton = document.getElementById("showStackerFormBtn");
  if (showFormButton) showFormButton.hidden = true;
  syncStackerEditState();
  document.getElementById("stName")?.focus();
}

function clearStackerForm(hideAfterClear = true) {
  editingStackerId = "";
  stackerDoubleEditorOpen = false;
  ["stName", "stDob", "stAge", "stDivision", "stCustomDivision", "stOrg", "stRegion", "stEmail", "stPhone"].forEach(id => setValue(id, ""));
  setValue("stGender", "M");
  setValue("stCountry", "Malaysia");
  setValue("stPaid", "No");
  setValue("stCheckedIn", "Yes");
  const special = document.getElementById("stSpecial");
  if (special) special.checked = false;
  updateStackerDivisionPreview();
  syncStackerEditState();
  if (!hideAfterClear) return;
  stackerFormVisible = false;
  const form = document.getElementById("stackerForm");
  if (form) form.hidden = true;
  const showFormButton = document.getElementById("showStackerFormBtn");
  if (showFormButton) showFormButton.hidden = false;
  focusStackerListAfterRender = true;
  document.getElementById("stackerListHeading")?.focus();
}

function requestDeleteStacker(id) {
  const stacker = state.stackers.find(item => item.id === id);
  if (!stacker) return;
  pendingDeleteStackerId = id;
  const teamCount = doublesForStacker(id).length;
  const resultCount = state.results.filter(r => r.participant === id).length;
  document.getElementById("deleteStackerTitle").textContent = `Delete ${stacker.id} ${stacker.name}?`;
  document.getElementById("deleteStackerText").textContent = `This will also remove ${teamCount} related team(s) and ${resultCount} result record(s).`;
  document.getElementById("deleteStackerConfirm").hidden = false;
}

function closeDeleteStackerConfirmation() {
  pendingDeleteStackerId = "";
  const box = document.getElementById("deleteStackerConfirm");
  if (box) box.hidden = true;
}

async function deleteStacker(id) {
  if (!id) return;
  const stacker = state.stackers.find(item => item.id === id);
  if (!stacker?.sqlId || !selectedSqlCompetitionId) return;
  setSaveStatus("Saving...", "saving");
  try {
    await stackerApi.delete(selectedSqlCompetitionId, stacker.sqlId);
  } catch (error) {
    flashMessage = { type: "error", text: `Save Failed: ${error.message}` };
    setSaveStatus("Save Failed", "failed");
    return;
  }
  state.stackers = state.stackers.filter(s => s.id !== id);
  state.doubles = state.doubles.filter(d => !registeredDoubleMemberIds(d).includes(id));
  state.relays = state.relays.map(team => ({ ...team, members: relayMemberIds(team).filter(member => member !== id) }));
  state.results = state.results.filter(r => r.participant !== id);
  appendCompetitionAuditLog({
    action: "stacker.deleted",
    entityType: "Stacker",
    entityId: id,
    summary: `Stacker ${id} deleted.`,
    before: stacker,
    after: null
  });
  await persistCompetitionAuditLog();
  if (editingStackerId === id) editingStackerId = "";
  pendingDeleteStackerId = "";
  flashMessage = { type: "success", text: `Stacker ${id} was removed.` };
  setSaveStatus("Saved", "saved");
}

function saveStackerDoubleAssignment() {
  if (!editingStackerId) return;
  const currentTeam = doublesForStacker(editingStackerId)[0];
  const before = auditSnapshot(currentTeam);
  const status = val("stDoubleStatus") || "complete";
  const partnerId = status === "pending" ? "" : selectedStackerId("stDoublePartner");
  const parentName = val("stDoubleParentName").trim();
  const type = parentName ? "child_parent" : "normal";
  const customDivision = val("stDoubleDivision").trim();
  const validation = validateDoubleEntry({ type, status, one: editingStackerId, two: partnerId, parentName });
  if (validation) {
    flashMessage = { type: "error", text: validation };
    renderStackers();
    return;
  }
  const displaced = removeConflictingDoubles([editingStackerId, partnerId].filter(Boolean), currentTeam?.id || "");
  const first = state.stackers.find(stacker => stacker.id === editingStackerId) || {};
  const second = state.stackers.find(stacker => stacker.id === partnerId) || {};
  const team = {
    id: currentTeam?.id || nextTeamCode("2"),
    type,
    status,
    one: editingStackerId,
    two: partnerId,
    parentName,
    customDivision,
    division: customDivision || generatedDoublesDivision(type, editingStackerId, partnerId),
    country: first.country || second.country || "Malaysia"
  };
  if (currentTeam) {
    state.doubles = state.doubles.map(existing => existing.id === currentTeam.id ? team : existing);
  } else {
    state.doubles.push(team);
  }
  appendCompetitionAuditLog({
    action: currentTeam ? "doubles.updated" : "doubles.created",
    entityType: "Doubles",
    entityId: team.id,
    summary: `${team.id} ${participantName("Doubles", team.id)} saved from stacker profile.`,
    before,
    after: team
  });
  flashMessage = {
    type: "success",
    text: `${team.id} ${participantName("Doubles", team.id)} was saved${displaced.length ? `; removed from ${displaced.join(", ")}.` : "."}`
  };
  stackerDoubleEditorOpen = false;
  renderStackers();
}

function addDouble() {
  const type = detectedDoubleType();
  const status = val("doubleStatus") || "complete";
  const one = selectedStackerId("doubleOne");
  const two = status === "pending" ? "" : selectedStackerId("doubleTwo");
  const parentName = val("doubleParentName").trim();
  const customDivision = val("doubleDivision").trim();
  const validation = validateDoubleEntry({ type, status, one, two, parentName });
  if (validation) {
    doubleFlashMessage = { type: "error", text: validation };
    return;
  }

  const first = state.stackers.find(stacker => stacker.id === one) || {};
  const second = state.stackers.find(stacker => stacker.id === two) || {};
  const displaced = removeConflictingDoubles([one, two].filter(Boolean), editingDoubleId);
  const before = auditSnapshot(editingDoubleId ? state.doubles.find(item => item.id === editingDoubleId) : null);
  const team = {
    id: editingDoubleId || nextTeamCode("2"),
    type,
    status,
    one,
    two,
    parentName,
    customDivision,
    division: customDivision || generatedDoublesDivision(type, one, two),
    country: first.country || second.country || "Malaysia"
  };
  if (editingDoubleId) {
    state.doubles = state.doubles.map(existing => existing.id === editingDoubleId ? team : existing);
  } else {
    state.doubles.push(team);
  }
  appendCompetitionAuditLog({
    action: editingDoubleId ? "doubles.updated" : "doubles.created",
    entityType: "Doubles",
    entityId: team.id,
    summary: `${team.id} ${participantName("Doubles", team.id)} ${editingDoubleId ? "updated" : "created"}.`,
    before,
    after: team
  });
  doublesTab = status === "pending" ? "incomplete" : "completed";
  doubleFlashMessage = { type: "success", text: `${team.id} ${participantName("Doubles", team.id)} was ${editingDoubleId ? "updated" : "added"}${displaced.length ? `; removed from ${displaced.join(", ")}.` : "."}` };
  clearDoubleForm(false);
}

function clearDoubleForm(renderNow = true) {
  editingDoubleId = "";
  ["doubleOneSearch", "doubleTwoSearch", "doubleOne", "doubleTwo", "doubleParentName", "doubleDivision"].forEach(id => setValue(id, ""));
  setValue("doubleStatus", "complete");
  setValue("doubleType", "Normal Doubles");
  if (renderNow) renderDoubles();
}

function loadDoubleForEdit(id) {
  const team = state.doubles.find(item => item.id === id);
  if (!team) return;
  editingDoubleId = id;
  setValue("doubleOneSearch", "");
  setValue("doubleTwoSearch", "");
  populateDoubleSelects();
  setValue("doubleOne", team.one || "");
  setValue("doubleTwo", team.two || "");
  setValue("doubleParentName", team.parentName || "");
  setValue("doubleStatus", team.status || "complete");
  setValue("doubleDivision", team.customDivision || "");
  updateDoubleFormMode();
  syncDoubleEditState();
  document.getElementById("doubleOne")?.focus();
}

function selectedStackerId(id) {
  const value = val(id);
  if (!value || value === "--") return "";
  return value.split(" - ")[0];
}

function validateDoubleEntry({ type, status, one, two, parentName }) {
  if (!one) return "Please choose the first stacker.";
  if (type === "normal" && status === "complete" && !two) return "Normal doubles needs two registered stackers.";
  if (type === "child_parent" && !two && !parentName) return "Child/Parent needs a registered parent or external parent name.";
  if (two && one === two) return "A stacker cannot partner with themselves.";
  return "";
}

function removeConflictingDoubles(stackerIds, exceptTeamId = "") {
  const displaced = [];
  state.doubles = state.doubles.filter(team => {
    if (team.id === exceptTeamId) return true;
    const conflicts = registeredDoubleMemberIds(team).some(id => stackerIds.includes(id));
    if (conflicts) displaced.push(team.id);
    return !conflicts;
  });
  return displaced;
}

function deleteDouble(id) {
  const team = state.doubles.find(item => item.id === id);
  if (!team) return;
  const teamName = participantName("Doubles", id);
  if (!confirm(`Delete ${team.id} ${participantName("Doubles", team.id)}?`)) return;
  state.doubles = state.doubles.filter(d => d.id !== id);
  appendCompetitionAuditLog({
    action: "doubles.deleted",
    entityType: "Doubles",
    entityId: id,
    summary: `${id} ${teamName} deleted.`,
    before: team,
    after: null
  });
}

function addRelay() {
  const selectedMembers = selectedRelayMemberIds().filter(Boolean);
  const relayName = val("relayName").trim();
  const validation = validateRelayEntry(selectedMembers, relayName);
  if (validation) {
    relayFlashMessage = { type: "error", text: validation };
    return;
  }
  const members = [...new Set(selectedMembers)];
  const displaced = removeConflictingRelays(members, editingRelayId);
  const before = auditSnapshot(editingRelayId ? state.relays.find(item => item.id === editingRelayId) : null);
  const team = {
    id: editingRelayId || nextTeamCode("3"),
    name: relayName,
    coordinator: val("relayCoordinator").trim(),
    email: val("relayEmail").trim(),
    phone: val("relayPhone").trim(),
    timedRelayDivision: val("timedRelayDivision").trim() || generatedRelayDivision(members),
    headToHeadDivision: val("headToHeadDivision").trim() || generatedRelayDivision(members, "headToHeadRelay"),
    customDivision: val("timedRelayDivision").trim(),
    division: val("timedRelayDivision").trim() || generatedRelayDivision(members),
    country: relayCountryForMembers(members),
    region: val("relayRegion").trim() || relayRegionForMembers(members),
    members
  };
  if (editingRelayId) {
    state.relays = state.relays.map(existing => existing.id === editingRelayId ? team : existing);
  } else {
    state.relays.push(team);
  }
  appendCompetitionAuditLog({
    action: editingRelayId ? "relay.updated" : "relay.created",
    entityType: "Relay",
    entityId: team.id,
    summary: `${team.id} ${participantName("Timed Relay", team.id)} ${editingRelayId ? "updated" : "created"}.`,
    before,
    after: team
  });
  relayTab = relayTeamStatus(team) === "Ready" ? "ready" : relayTeamStatus(team).toLowerCase();
  relayFlashMessage = { type: "success", text: `${team.id} ${participantName("Timed Relay", team.id)} was ${editingRelayId ? "updated" : "added"}${displaced.length ? `; removed from ${displaced.join(", ")}.` : "."}` };
  clearRelayForm(false);
}

function validateRelayEntry(members, relayName = "") {
  if (!relayName) return "Relay team name is required.";
  const duplicateName = state.relays.some(team => team.id !== editingRelayId && String(team.name || "").trim().toLowerCase() === relayName.toLowerCase());
  if (duplicateName) return "Relay team name must be unique.";
  if (members.length > 6) return "Relay team can only keep up to 6 registered stackers.";
  const duplicate = members.find((id, index) => members.indexOf(id) !== index);
  if (duplicate) return "Each stacker can only be selected once in the same relay team.";
  return "";
}

function removeConflictingRelays(stackerIds, exceptTeamId = "") {
  const displaced = [];
  state.relays = state.relays.map(team => {
    if (team.id === exceptTeamId) return team;
    const originalMembers = relayMemberIds(team);
    const nextMembers = originalMembers.filter(id => !stackerIds.includes(id));
    if (nextMembers.length !== originalMembers.length) displaced.push(team.id);
    return { ...team, members: nextMembers };
  });
  return [...new Set(displaced)];
}

function loadRelayForEdit(id) {
  const team = state.relays.find(item => item.id === id);
  if (!team) return;
  editingRelayId = id;
  setValue("relayName", team.name || "");
  setValue("relayCoordinator", team.coordinator || "");
  setValue("relayEmail", team.email || "");
  setValue("relayPhone", team.phone || "");
  setValue("timedRelayDivision", team.timedRelayDivision || team.customDivision || team.division || "");
  setValue("headToHeadDivision", team.headToHeadDivision || "");
  setValue("relayRegion", team.region || "");
  for (let slot = 1; slot <= 6; slot += 1) setValue(`relayMemberSearch${slot}`, "");
  populateRelaySelects();
  relayMemberIds(team).forEach((member, index) => setValue(`relayMember${index + 1}`, member));
  syncRelayEditState();
  showSelectedRelayWarnings();
  document.getElementById("relayName")?.focus();
}

function clearRelayForm(renderNow = true) {
  editingRelayId = "";
  ["relayName", "relayCoordinator", "relayEmail", "relayPhone", "timedRelayDivision", "headToHeadDivision", "relayRegion"].forEach(id => setValue(id, ""));
  for (let slot = 1; slot <= 6; slot += 1) {
    setValue(`relayMemberSearch${slot}`, "");
    setValue(`relayMember${slot}`, "");
  }
  if (renderNow) renderRelay();
}

function deleteRelay(id) {
  const team = state.relays.find(item => item.id === id);
  if (!team) return;
  const teamName = participantName("Timed Relay", id);
  if (!confirm(`Delete ${team.id} ${participantName("Timed Relay", team.id)}?`)) return;
  state.relays = state.relays.filter(relay => relay.id !== id);
  state.results = state.results.filter(result => !(["Timed Relay", "Relay"].includes(result.type) && result.participant === id));
  appendCompetitionAuditLog({
    action: "relay.deleted",
    entityType: "Relay",
    entityId: id,
    summary: `${id} ${teamName} deleted.`,
    before: team,
    after: null
  });
}

function relayForStacker(stackerId) {
  return state.relays.find(team => relayMemberIds(team).includes(stackerId));
}

function generatedRelayDivision(members, relayGroup = "timedRelay") {
  const stackers = members.map(id => state.stackers.find(stacker => stacker.id === id)).filter(Boolean);
  if (!stackers.length) return "Open";
  const oldestAge = Math.max(...stackers.map(stacker => Number(ageOnCompetitionDate(stacker.dob, state.settings.start) || stacker.age || 0)));
  if (!Number.isFinite(oldestAge) || oldestAge <= 0) return "Open";
  const cutoff = [...(state.divisionSettings?.[relayGroup] || [])].sort((a, b) => Number(a) - Number(b)).find(age => oldestAge <= Number(age));
  return cutoff ? `${cutoff}U` : "Open";
}

function generatedTeamCutoffDivision(age, group, prefix = "") {
  if (!Number.isFinite(age) || age <= 0) return "Open";
  const cutoff = [...(state.divisionSettings?.[group] || [])].sort((a, b) => Number(a) - Number(b)).find(value => age <= Number(value));
  return cutoff ? `${prefix}${cutoff}U` : "Open";
}

function teamDivisionRanges(cutoffs, prefix = "") {
  const sorted = [...cutoffs].sort((a, b) => Number(a) - Number(b));
  return sorted.map(age => `${prefix}${age}U`);
}

function relayDivision(team) {
  return relayTimedDivision(team);
}

function relayTimedDivision(team) {
  return generatedRelayDivision(relayMemberIds(team), "timedRelay");
}

function relayHeadToHeadDivision(team) {
  return generatedRelayDivision(relayMemberIds(team), "headToHeadRelay");
}

function relayCountryForMembers(members) {
  return members.map(id => state.stackers.find(stacker => stacker.id === id)?.country).find(Boolean) || "Malaysia";
}

function relayRegionForMembers(members) {
  return members.map(id => state.stackers.find(stacker => stacker.id === id)?.region || state.stackers.find(stacker => stacker.id === id)?.org).find(Boolean) || "";
}

function relayLocation(team) {
  return team.region || team.org || team.country || relayRegionForMembers(relayMemberIds(team)) || "--";
}

function generatedDoublesDivision(type, one, two = "") {
  const first = state.stackers.find(stacker => stacker.id === one);
  const second = state.stackers.find(stacker => stacker.id === two);
  const maxAge = Math.max(
    Number(first?.age || ageOnCompetitionDate(first?.dob, state.settings.start) || 0),
    Number(second?.age || ageOnCompetitionDate(second?.dob, state.settings.start) || 0)
  );
  if (type === "child_parent") {
    if (first?.special === "Yes") return generatedTeamCutoffDivision(maxAge, "specialChildParentDoubles", "SS Child/Parent ");
    return generatedTeamCutoffDivision(maxAge, "childParentDoubles", "Child/Parent ");
  }
  if (first?.special === "Yes" || second?.special === "Yes") return generatedTeamCutoffDivision(maxAge, "specialDoubles", "SS ");
  return generatedTeamCutoffDivision(maxAge, "doubles");
}

function saveResult() {
  const type = val("entryType");
  const participant = val("participantSelect").split(" - ")[0];
  const attempts = [Number(val("attempt1")), Number(val("attempt2")), Number(val("attempt3"))].filter(n => n > 0);
  if (!participant || !attempts.length) return;
  const result = { id: crypto.randomUUID(), stage: val("resultStage"), type, participant, event: val("resultEvent"), attempts, penalty: Number(val("penalty")) };
  state.results.push(result);
  appendCompetitionAuditLog({
    action: "results.created",
    entityType: "Result",
    entityId: result.id,
    summary: `${result.stage} ${result.event} result created for ${result.participant}.`,
    before: null,
    after: result
  });
}

function countBy(key, value) {
  return state.stackers.filter(s => s[key] === value).length;
}

function groupCounts(items, key) {
  return items.reduce((acc, item) => {
    acc[item[key]] = (acc[item[key]] || 0) + 1;
    return acc;
  }, {});
}

function participantName(type, id) {
  if (type === "Doubles") {
    const d = findDoublesTeam(id);
    return d ? doubleTeamName(d) : "Unknown Team";
  }
  if (type === "Timed Relay" || type === "Relay") {
    const relay = state.relays.find(team => team.id === id);
    if (!relay) return "Unknown Relay Team";
    const members = relayMemberIds(relay).map(stackerName).filter(name => name !== "Unknown");
    return relay.name || members.join(" / ") || id;
  }
  return stackerName(id);
}

function relayMemberIds(team) {
  if (Array.isArray(team?.members)) return team.members.filter(Boolean);
  return [team?.one, team?.two, team?.three, team?.four, team?.five, team?.six].filter(Boolean);
}

function stackerName(id) {
  return state.stackers.find(s => s.id === id)?.name || "Unknown";
}

function stackerPickerLabel(stacker, status = "") {
  const age = stacker.age || ageOnCompetitionDate(stacker.dob, state.settings.start) || "";
  const ageGender = [age ? `${age}` : "", stacker.gender || ""].filter(Boolean).join(" ");
  const division = stacker.division || stacker.standardDivision || stacker.customDivision || "Open";
  const location = stacker.org || stacker.region || stacker.country || "Independent";
  return [
    `${stacker.id} - ${stacker.name || "Unnamed"}`,
    ageGender || "Age --",
    division,
    location,
    status
  ].filter(Boolean).join(" // ");
}

function stackerPickerSearchText(stacker) {
  return [
    stacker.id,
    stacker.name,
    stacker.gender,
    stacker.age,
    stacker.dob,
    stacker.division,
    stacker.standardDivision,
    stacker.customDivision,
    stacker.org,
    stacker.region,
    stacker.country
  ].filter(Boolean).join(" ").toLowerCase();
}

function doubleTeamName(team) {
  const first = stackerName(team.one);
  if (team.status === "pending") return `${first} & ${team.parentName || "Need Partner"}`;
  const partner = team.two ? stackerName(team.two) : team.parentName;
  return `${first} & ${partner || "Need Partner"}`;
}

function doublePartnerNameForStacker(team, stackerId) {
  if (team.one === stackerId) return team.two ? stackerName(team.two) : team.parentName || "Need Partner";
  if (team.two === stackerId) return stackerName(team.one);
  return participantName("Doubles", team.id);
}

function doubleTypeLabel(team) {
  return team.type === "child_parent" ? "Child / Parent" : "Normal Doubles";
}

function doubleStatusLabel(team) {
  return team.status === "pending" ? "Need Partner" : "Complete";
}

function doubleDivision(team) {
  return team.customDivision || team.division || generatedDoublesDivision(team.type || "normal", team.one, team.two);
}

function teamCountry(team) {
  const first = state.stackers.find(stacker => stacker.id === team.one);
  const second = state.stackers.find(stacker => stacker.id === team.two);
  return team.country || first?.country || second?.country || "Malaysia";
}

function ageOnCompetitionDate(dobValue, competitionStart = "", mode = ageCalculationMode) {
  const normalizedDob = normalizedDateValue(dobValue);
  const normalizedEventDate = normalizedDateValue(competitionStart) || new Date().toISOString().slice(0, 10);
  if (!normalizedDob) return 0;
  const dob = new Date(`${normalizedDob}T00:00:00`);
  const eventDate = new Date(`${normalizedEventDate}T00:00:00`);
  if (Number.isNaN(dob.getTime()) || Number.isNaN(eventDate.getTime())) return 0;
  if (mode === "yearBorn") return Math.max(eventDate.getFullYear() - dob.getFullYear(), 0);
  let age = eventDate.getFullYear() - dob.getFullYear();
  const beforeBirthday = eventDate.getMonth() < dob.getMonth()
    || (eventDate.getMonth() === dob.getMonth() && eventDate.getDate() < dob.getDate());
  if (beforeBirthday) age -= 1;
  return Math.max(age, 0);
}

function normalizedDateValue(value) {
  if (!value) return "";
  const raw = String(value).trim();
  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(raw);
  if (iso) return buildDateValue(Number(iso[1]), Number(iso[2]), Number(iso[3]));

  const slashed = /^(\d{1,2})[\/.-](\d{1,2})[\/.-](\d{4})$/.exec(raw);
  if (slashed) {
    const first = Number(slashed[1]);
    const second = Number(slashed[2]);
    const year = Number(slashed[3]);
    if (first > 12) return buildDateValue(year, second, first);
    if (second > 12) return buildDateValue(year, first, second);
    return buildDateValue(year, second, first);
  }

  const monthNamed = /^([A-Za-z]+)\s+(\d{1,2}),?\s+(\d{4})$/.exec(raw);
  if (monthNamed) {
    const month = monthNames.indexOf(monthNamed[1].toLowerCase()) + 1;
    if (month > 0) return buildDateValue(Number(monthNamed[3]), month, Number(monthNamed[2]));
  }

  return "";
}

function buildDateValue(year, month, day) {
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return "";
  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function nextStackerCode() {
  return nextTeamCode("1");
}

function nextTeamCode(prefix) {
  const max = state.stackers
    .concat(state.doubles)
    .concat(state.relays || [])
    .map(item => new RegExp(`^${prefix}\\.(\\d+)$`).exec(item.id)?.[1])
    .filter(Boolean)
    .map(Number)
    .reduce((highest, value) => Math.max(highest, value), 0);
  return `${prefix}.${max + 1}`;
}

function calculateBestResult(result) {
  return BestResultEngine.calculateBestResult(result);
}

function bestAttempt(result) {
  const value = BestResultEngine.bestTime(result);
  return Number.isFinite(value) ? value : Infinity;
}

function official(result) {
  return BestResultEngine.rankingTime(result);
}

function resultStatusLabel(result) {
  const summary = calculateBestResult(result);
  if (summary.status === "scratch") return "Scratch";
  if (summary.status === "missing") return "Missing";
  if (summary.status === "invalid") return "Invalid";
  return Number(result?.penalty) ? `+${result.penalty}` : "--";
}

function bestResults() {
  return [...state.results].filter(r => official(r) !== Infinity).sort((a, b) => official(a) - official(b));
}

function fmt(value) {
  return Number.isFinite(value) ? `${value.toFixed(3)}s` : "--";
}

function setOptions(id, options) {
  const el = document.getElementById(id);
  if (!el) return;
  el.innerHTML = options.map(option => `<option>${esc(option)}</option>`).join("");
}

function setValue(id, value) {
  const el = document.getElementById(id);
  if (el) el.value = value;
}


function todayIsoDate() {
  return stackMeetDateOnly();
}

// Formats browser-generated timestamps in StackMeet's fixed GMT+8 operating timezone.
function stackMeetDateTime(value = new Date()) {
  return new Intl.DateTimeFormat(stackMeetLocale, {
    timeZone: stackMeetTimeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    timeZoneName: "short"
  }).format(value);
}

// Parses UTC timestamps that may arrive from SQL JSON without an explicit offset.
// Parses an audit timestamp safely before formatting it in StackMeet's configured timezone.
function parseUtcDate(value) {
  if (!value) return new Date();
  const textValue = String(value);
  const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(textValue) ? textValue : `${textValue}Z`);
  return Number.isNaN(date.getTime()) ? new Date() : date;
}

// Returns today's date for defaults using GMT+8 instead of the browser's local timezone.
function stackMeetDateOnly(value = new Date()) {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: stackMeetTimeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).format(value);
}

function isoDateValue(value) {
  const text = String(value || "").trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return text;
  const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text);
  if (!match) return text;
  return `${match[3]}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}`;
}
function val(id) {
  return document.getElementById(id)?.value || "";
}

function stateToXml(data) {
  const node = (name, value = "", attrs = "") => `<${name}${attrs}>${xmlEsc(value)}</${name}>`;
  const list = (name, items, renderItem) => `<${name}>\n${items.map(renderItem).join("\n")}\n</${name}>`;
  const stateJson = node("stateJson", JSON.stringify(data));
  const settings = `<settings>\n${Object.entries(data.settings).map(([key, value]) => node(key, value)).join("\n")}\n</settings>`;
  const leaderboard = `<leaderboard>\n${Object.entries(data.leaderboard).map(([key, value]) => node(key, value)).join("\n")}\n</leaderboard>`;
  const awards = `<awards>\n${node("individualPlaces", data.awards?.individualPlaces)}\n<individualItems>${(data.awards?.individualItems || []).map(item => node("item", item)).join("")}</individualItems>\n${node("doublesPlaces", data.awards?.doublesPlaces)}\n<doublesItems>${(data.awards?.doublesItems || []).map(item => node("item", item)).join("")}</doublesItems>\n${node("relayPlaces", data.awards?.relayPlaces)}\n${node("relayUnits", data.awards?.relayUnits)}\n<relayItems>${(data.awards?.relayItems || []).map(item => node("item", item)).join("")}</relayItems>\n<overall>${awardOverallGroups.map(group => `<group key="${xmlAttr(group.key)}">${node("limit", data.awards?.overall?.[group.key]?.limit)}${node("item", data.awards?.overall?.[group.key]?.item)}</group>`).join("")}</overall>\n</awards>`;
  const qualificationSnapshots = list("finalQualificationSnapshots", data.finalQualificationSnapshots || [], snapshot => `<snapshot id="${xmlAttr(snapshot.id)}">${node("payload", JSON.stringify(snapshot))}</snapshot>`);
  const events = `<events>\n${Object.entries(data.events).map(([group, values]) => `<group name="${xmlAttr(group)}">${values.map(value => node("event", value)).join("")}</group>`).join("\n")}\n</events>`;
  const divisionSettings = `<divisionSettings>\n${["combined", "male", "female", "special", "doubles", "childParentDoubles", "specialDoubles", "specialChildParentDoubles", "timedRelay", "headToHeadRelay"].map(group => `<group name="${group}">${(data.divisionSettings?.[group] || []).map(age => node("age", age)).join("")}</group>`).join("\n")}\n<custom>${(data.divisionSettings?.custom || []).map(value => node("division", value)).join("")}</custom>\n</divisionSettings>`;
  const divisions = list("divisions", data.divisions, division => node("division", division));
  const translations = `<translations>\n${Object.entries(data.translations || {}).map(([code, values]) => `<lang code="${xmlAttr(code)}">${Object.entries(values || {}).map(([key, value]) => node("item", value, ` key="${xmlAttr(key)}"`)).join("")}</lang>`).join("\n")}\n</translations>`;
  const stackers = list("stackers", data.stackers, s => `<stacker id="${xmlAttr(s.id)}">${node("name", s.name)}${node("gender", s.gender)}${node("dob", s.dob)}${node("age", s.age)}${node("special", s.special || "No")}${node("org", s.org)}${node("division", s.division)}${node("customDivision", s.customDivision)}${node("standardDivision", s.standardDivision)}${node("country", s.country)}${node("region", s.region)}${node("email", s.email)}${node("phone", s.phone)}${node("paid", s.paid)}${node("checkedIn", s.checkedIn)}</stacker>`);
  const doubles = list("doubles", data.doubles, d => `<team id="${xmlAttr(d.id)}">${node("type", d.type || "normal")}${node("status", d.status || "complete")}${node("one", d.one)}${node("two", d.two)}${node("parentName", d.parentName)}${node("customDivision", d.customDivision)}${node("division", doubleDivision(d))}${node("org", d.org)}${node("country", d.country || teamCountry(d))}${node("region", d.region || teamRegion(d))}</team>`);
  const relays = list("relays", data.relays || [], relay => `<team id="${xmlAttr(relay.id)}">${node("name", relay.name)}${node("coordinator", relay.coordinator)}${node("email", relay.email)}${node("phone", relay.phone)}${node("timedRelayDivision", relay.timedRelayDivision || relayDivision(relay))}${node("headToHeadDivision", relay.headToHeadDivision)}${node("customDivision", relay.customDivision)}${node("division", relayDivision(relay))}${node("org", relay.org)}${node("country", relay.country)}${node("region", relay.region)}<members>${relayMemberIds(relay).map(member => node("member", member)).join("")}</members></team>`);
  const results = list("results", data.results, r => `<result id="${xmlAttr(r.id)}">${node("stage", r.stage)}${node("type", r.type)}${node("participant", r.participant)}${node("event", r.event)}<attempts>${r.attempts.map(a => node("attempt", a)).join("")}</attempts>${node("penalty", r.penalty)}</result>`);
  const notifications = list("notifications", data.notifications, n => `<notification id="${xmlAttr(n.id)}" read="${xmlAttr(n.read ? "true" : "false")}">${node("title", n.title)}${node("time", n.time)}</notification>`);
  const users = list("users", data.users, u => `<user>${node("name", u.name)}${node("access", u.access)}${node("last", u.last)}${node("platform", u.platform)}${node("browser", u.browser)}</user>`);
  return `<?xml version="1.0" encoding="UTF-8"?>\n<stackmeet version="2">\n${stateJson}\n${settings}\n${leaderboard}\n${awards}\n${qualificationSnapshots}\n${events}\n${divisionSettings}\n${divisions}\n${translations}\n${stackers}\n${doubles}\n${relays}\n${results}\n${notifications}\n${users}\n</stackmeet>\n`;
}

function xmlToState(xmlText) {
  const doc = new DOMParser().parseFromString(xmlText, "application/xml");
  if (doc.querySelector("parsererror") || !doc.querySelector("stackmeet")) {
    throw new Error("Invalid XML");
  }
  const text = (root, selector, fallback = "") => root.querySelector(selector)?.textContent ?? fallback;
  const stateJson = doc.querySelector("stackmeet > stateJson")?.textContent;
  if (stateJson) return normalizeState(JSON.parse(stateJson));
  const imported = structuredClone(demo);
  const settings = doc.querySelector("settings");
  const leaderboard = doc.querySelector("leaderboard");
  const awards = doc.querySelector("awards");
  if (settings) Object.keys(imported.settings).forEach(key => imported.settings[key] = text(settings, key, imported.settings[key]));
  if (leaderboard) Object.keys(imported.leaderboard).forEach(key => imported.leaderboard[key] = text(leaderboard, key, imported.leaderboard[key]));
  if (awards) {
    imported.awards = {
      individualPlaces: Number(text(awards, "individualPlaces", defaultAwards.individualPlaces)),
      individualItems: [...awards.querySelectorAll("individualItems item")].map(item => item.textContent),
      doublesPlaces: Number(text(awards, "doublesPlaces", defaultAwards.doublesPlaces)),
      doublesItems: [...awards.querySelectorAll("doublesItems item")].map(item => item.textContent),
      relayPlaces: Number(text(awards, "relayPlaces", defaultAwards.relayPlaces)),
      relayUnits: Number(text(awards, "relayUnits", defaultAwards.relayUnits)),
      relayItems: [...awards.querySelectorAll("relayItems item")].map(item => item.textContent),
      overall: Object.fromEntries([...awards.querySelectorAll("overall group")].map(group => [group.getAttribute("key"), {
        limit: Number(text(group, "limit", 0)),
        item: text(group, "item", "Trophy")
      }]))
    };
  }
  imported.finalQualificationSnapshots = [...doc.querySelectorAll("finalQualificationSnapshots snapshot payload")].map(node => {
    try { return JSON.parse(node.textContent); } catch { return null; }
  }).filter(Boolean);
  imported.settings.advanceIndividuals = Number(imported.settings.advanceIndividuals || 0);
  imported.settings.advanceDoubles = Number(imported.settings.advanceDoubles || 0);
  imported.leaderboard.limit = Number(imported.leaderboard.limit || 10);
  imported.events = {};
  doc.querySelectorAll("events group").forEach(group => {
    imported.events[group.getAttribute("name")] = [...group.querySelectorAll("event")].map(event => event.textContent);
  });
  const divisionSettingsNode = doc.querySelector("divisionSettings");
  if (divisionSettingsNode) {
    imported.divisionSettings = structuredClone(defaultDivisionSettings);
    divisionSettingsNode.querySelectorAll("group").forEach(group => {
      imported.divisionSettings[group.getAttribute("name")] = [...group.querySelectorAll("age")].map(age => Number(age.textContent)).filter(Number.isFinite);
    });
    imported.divisionSettings.custom = [...divisionSettingsNode.querySelectorAll("custom division")].map(node => node.textContent);
  }
  imported.divisions = [...doc.querySelectorAll("divisions division")].map(node => node.textContent);
  imported.translations = Object.fromEntries(Object.entries(defaultTranslationPacks).map(([code, pack]) => [code, structuredClone(pack)]));
  doc.querySelectorAll("translations lang").forEach(lang => {
    const code = lang.getAttribute("code");
    if (!code || !defaultTranslationPacks[code]) return;
    lang.querySelectorAll("item").forEach(item => {
      const key = item.getAttribute("key");
      if (key) imported.translations[code][key] = item.textContent;
    });
  });
  imported.stackers = [...doc.querySelectorAll("stackers stacker")].map(s => ({
    id: s.getAttribute("id") || "",
    name: text(s, "name"),
    gender: text(s, "gender"),
    dob: text(s, "dob"),
    age: text(s, "age"),
    special: text(s, "special", "No"),
    org: text(s, "org"),
    division: text(s, "division"),
    customDivision: text(s, "customDivision"),
    standardDivision: text(s, "standardDivision"),
    country: text(s, "country"),
    region: text(s, "region"),
    email: text(s, "email"),
    phone: text(s, "phone"),
    paid: text(s, "paid", "No"),
    checkedIn: text(s, "checkedIn", "No")
  }));
  imported.doubles = [...doc.querySelectorAll("doubles team")].map(d => ({
    id: d.getAttribute("id") || "",
    type: text(d, "type", ""),
    status: text(d, "status", ""),
    one: text(d, "one"),
    two: text(d, "two"),
    parentName: text(d, "parentName"),
    customDivision: text(d, "customDivision"),
    division: text(d, "division"),
    org: text(d, "org"),
    country: text(d, "country"),
    region: text(d, "region")
  }));
  imported.relays = [...doc.querySelectorAll("relays team")].map(relay => ({
    id: relay.getAttribute("id") || "",
    name: text(relay, "name"),
    coordinator: text(relay, "coordinator"),
    email: text(relay, "email"),
    phone: text(relay, "phone"),
    timedRelayDivision: text(relay, "timedRelayDivision"),
    headToHeadDivision: text(relay, "headToHeadDivision"),
    customDivision: text(relay, "customDivision"),
    division: text(relay, "division"),
    org: text(relay, "org"),
    country: text(relay, "country"),
    region: text(relay, "region"),
    members: [...relay.querySelectorAll("members member")].map(member => member.textContent).filter(Boolean)
  }));
  imported.results = [...doc.querySelectorAll("results result")].map(r => ({
    id: r.getAttribute("id") || crypto.randomUUID(),
    stage: text(r, "stage"),
    type: text(r, "type"),
    participant: text(r, "participant"),
    event: text(r, "event"),
    attempts: [...r.querySelectorAll("attempt")].map(a => Number(a.textContent)).filter(Number.isFinite),
    penalty: Number(text(r, "penalty", "0"))
  }));
  imported.notifications = [...doc.querySelectorAll("notifications notification")].map(n => ({
    id: n.getAttribute("id") || crypto.randomUUID(),
    title: text(n, "title"),
    time: text(n, "time"),
    read: n.getAttribute("read") === "true"
  }));
  imported.users = [...doc.querySelectorAll("users user")].map(u => ({
    name: text(u, "name"),
    access: text(u, "access"),
    last: text(u, "last"),
    platform: text(u, "platform"),
    browser: text(u, "browser")
  }));
  return normalizeState(imported);
}

function xmlEsc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

function xmlAttr(value) {
  return xmlEsc(value).replaceAll('"', "&quot;");
}

function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function cssEscape(value) {
  return String(value ?? "").replaceAll("\\", "\\\\").replaceAll('"', '\\"');
}

function showBootError(error) {
  const target = document.getElementById("loginError") || document.getElementById("view");
  if (target) target.textContent = error?.message || String(error || "Unable to start NADITrack.");
  document.body.classList.add("auth-pending");
}

async function initializeApplication() {
  applyBrandingChrome();
  const session = await window.StackMeetAuth.requireLogin();
  repository.setCompetitionKey(session.competitionId);
  state = await loadState();
  try {
    await initializeSqlNativeStackers();
  } catch (error) {
    console.error("Unable to initialize SQL-native stackers.", error);
    flashMessage = { type: "error", text: "Save Failed: SQL-native stackers are unavailable." };
  }
  render();
}

initializeApplication().catch(showBootError);
