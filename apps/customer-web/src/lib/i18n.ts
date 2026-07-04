// i18n UI khách — VI/EN/ZH/JA (PRD 5.4: không hard-code text trong backend;
// tên món dịch qua product_translations, UI strings dịch tại client)

export type Lang = "vi" | "en" | "zh" | "ja";

export const LANGS: { code: Lang; label: string; nativeName: string }[] = [
  { code: "vi", label: "VI", nativeName: "Tiếng Việt" },
  { code: "en", label: "EN", nativeName: "English" },
  { code: "zh", label: "中文", nativeName: "中文" },
  { code: "ja", label: "日本語", nativeName: "日本語" },
];

const vi = {
  loadingMenu: "Đang tải menu...",
  menuLoadError: "Không tải được menu.",
  table: "Bàn",
  callStaff: "🔔 Gọi nhân viên",
  requestPayment: "💰 Thanh toán",
  staffCalled: "Đã gọi nhân viên 🔔",
  paymentRequested: "Đã gửi yêu cầu thanh toán 💰",
  requestFailed: "Không gửi được yêu cầu.",
  searchPlaceholder: "Tìm món...",
  clearSearch: "Xóa tìm kiếm",
  openNow: "● Đang mở cửa",
  closedNow: "● Ngoài giờ",
  todayLabel: "Hôm nay",
  paymentLabel: "Thanh toán",
  cash: "Tiền mặt",
  bankTransfer: "Chuyển khoản QR",
  tableOrders: "Order của bàn",
  tableTotal: "Tổng bàn",
  noResults: "Không tìm thấy món nào cho",
  noResultsHint: "Thử từ khóa ngắn hơn nhé.",
  outOfStock: "Hết hàng",
  viewCart: "Xem giỏ hàng",
  cartTitle: "Giỏ hàng — Bàn",
  totalLabel: "Tổng cộng",
  submitOrder: "Gửi order",
  submittingOrder: "Đang gửi...",
  orderFailed: "Gửi order thất bại. Vui lòng thử lại.",
  size: "Size",
  maxSelect: "chọn tối đa",
  notePlaceholder: "Ghi chú (VD: ít đá)",
  addLabel: "Thêm",
  paymentModalTitle: "Thanh toán — Bàn",
  scanQrHint: "Quét QR để chuyển khoản, hoặc thanh toán tiền mặt. Nhân viên sẽ đến xác nhận với bạn.",
  staffWillCollect: "Nhân viên sẽ đến thu tiền tại bàn. Cảm ơn bạn! 🙏",
  close: "Đóng",
  footerTagline: "Quét QR · Gọi món · Không chờ đợi",
  footerHours: "Giờ mở cửa hôm nay",
  status_submitted: "Đã gửi — chờ quán nhận",
  status_confirmed: "Quán đã nhận ✓",
  status_preparing: "Đang chuẩn bị 👨‍🍳",
  status_ready: "Món đã sẵn sàng 🔔",
  status_completed: "Hoàn thành ✓",
  status_rejected: "Quán từ chối",
  status_cancelled: "Đã hủy",
};

export type MsgKey = keyof typeof vi;
type Dict = Record<MsgKey, string>;

const en: Dict = {
  loadingMenu: "Loading menu...",
  menuLoadError: "Could not load the menu.",
  table: "Table",
  callStaff: "🔔 Call staff",
  requestPayment: "💰 Pay bill",
  staffCalled: "Staff has been called 🔔",
  paymentRequested: "Payment request sent 💰",
  requestFailed: "Could not send the request.",
  searchPlaceholder: "Search dishes...",
  clearSearch: "Clear search",
  openNow: "● Open now",
  closedNow: "● Closed",
  todayLabel: "Today",
  paymentLabel: "Payment",
  cash: "Cash",
  bankTransfer: "Bank QR",
  tableOrders: "Table orders",
  tableTotal: "Table total",
  noResults: "No dishes found for",
  noResultsHint: "Try a shorter keyword.",
  outOfStock: "Sold out",
  viewCart: "View cart",
  cartTitle: "Cart — Table",
  totalLabel: "Total",
  submitOrder: "Place order",
  submittingOrder: "Sending...",
  orderFailed: "Failed to place order. Please try again.",
  size: "Size",
  maxSelect: "max",
  notePlaceholder: "Note (e.g. less ice)",
  addLabel: "Add",
  paymentModalTitle: "Payment — Table",
  scanQrHint: "Scan the QR to pay by bank transfer, or pay cash. Staff will confirm with you.",
  staffWillCollect: "Staff will collect payment at your table. Thank you! 🙏",
  close: "Close",
  footerTagline: "Scan QR · Order · No waiting",
  footerHours: "Today's opening hours",
  status_submitted: "Sent — waiting for the restaurant",
  status_confirmed: "Confirmed ✓",
  status_preparing: "Preparing 👨‍🍳",
  status_ready: "Ready 🔔",
  status_completed: "Completed ✓",
  status_rejected: "Rejected",
  status_cancelled: "Cancelled",
};

const zh: Dict = {
  loadingMenu: "菜单加载中...",
  menuLoadError: "无法加载菜单。",
  table: "桌号",
  callStaff: "🔔 呼叫服务员",
  requestPayment: "💰 结账",
  staffCalled: "已呼叫服务员 🔔",
  paymentRequested: "已发送结账请求 💰",
  requestFailed: "请求发送失败。",
  searchPlaceholder: "搜索菜品...",
  clearSearch: "清除搜索",
  openNow: "● 营业中",
  closedNow: "● 休息中",
  todayLabel: "今天",
  paymentLabel: "支付方式",
  cash: "现金",
  bankTransfer: "扫码转账",
  tableOrders: "本桌订单",
  tableTotal: "本桌合计",
  noResults: "未找到相关菜品：",
  noResultsHint: "请尝试更短的关键词。",
  outOfStock: "售罄",
  viewCart: "查看购物车",
  cartTitle: "购物车 — 桌号",
  totalLabel: "合计",
  submitOrder: "提交订单",
  submittingOrder: "提交中...",
  orderFailed: "下单失败，请重试。",
  size: "规格",
  maxSelect: "最多选",
  notePlaceholder: "备注（如：少冰）",
  addLabel: "加入",
  paymentModalTitle: "结账 — 桌号",
  scanQrHint: "扫码转账，或使用现金支付。服务员将前来确认。",
  staffWillCollect: "服务员将到桌前收款，谢谢！🙏",
  close: "关闭",
  footerTagline: "扫码点餐 · 无需等待",
  footerHours: "今日营业时间",
  status_submitted: "已发送 — 等待接单",
  status_confirmed: "已接单 ✓",
  status_preparing: "制作中 👨‍🍳",
  status_ready: "已完成 🔔",
  status_completed: "已完成 ✓",
  status_rejected: "已拒绝",
  status_cancelled: "已取消",
};

const ja: Dict = {
  loadingMenu: "メニューを読み込み中...",
  menuLoadError: "メニューを読み込めませんでした。",
  table: "テーブル",
  callStaff: "🔔 スタッフを呼ぶ",
  requestPayment: "💰 お会計",
  staffCalled: "スタッフを呼びました 🔔",
  paymentRequested: "お会計をリクエストしました 💰",
  requestFailed: "リクエストを送信できませんでした。",
  searchPlaceholder: "メニューを検索...",
  clearSearch: "検索をクリア",
  openNow: "● 営業中",
  closedNow: "● 営業時間外",
  todayLabel: "本日",
  paymentLabel: "お支払い",
  cash: "現金",
  bankTransfer: "QR振込",
  tableOrders: "テーブルの注文",
  tableTotal: "テーブル合計",
  noResults: "該当するメニューがありません：",
  noResultsHint: "短いキーワードでお試しください。",
  outOfStock: "売り切れ",
  viewCart: "カートを見る",
  cartTitle: "カート — テーブル",
  totalLabel: "合計",
  submitOrder: "注文する",
  submittingOrder: "送信中...",
  orderFailed: "注文に失敗しました。もう一度お試しください。",
  size: "サイズ",
  maxSelect: "最大",
  notePlaceholder: "メモ（例：氷少なめ）",
  addLabel: "追加",
  paymentModalTitle: "お会計 — テーブル",
  scanQrHint: "QRコードで振込、または現金でお支払いください。スタッフが確認に伺います。",
  staffWillCollect: "スタッフがお席までお伺いします。ありがとうございます！🙏",
  close: "閉じる",
  footerTagline: "QRで注文 · 待ち時間なし",
  footerHours: "本日の営業時間",
  status_submitted: "送信済み — お店の確認待ち",
  status_confirmed: "確認済み ✓",
  status_preparing: "調理中 👨‍🍳",
  status_ready: "できあがり 🔔",
  status_completed: "完了 ✓",
  status_rejected: "お断りされました",
  status_cancelled: "キャンセル済み",
};

const DICTS: Record<Lang, Dict> = { vi, en, zh, ja };

export function t(lang: Lang, key: MsgKey): string {
  return DICTS[lang][key] ?? vi[key];
}

const STORAGE_KEY = "tg_lang";

/** Ngôn ngữ khởi tạo: đã lưu > ngôn ngữ trình duyệt > vi */
export function getInitialLang(): Lang {
  if (typeof window === "undefined") return "vi";
  const saved = window.localStorage.getItem(STORAGE_KEY);
  if (saved && DICTS[saved as Lang]) return saved as Lang;
  const nav = (navigator.language || "vi").toLowerCase();
  if (nav.startsWith("en")) return "en";
  if (nav.startsWith("zh")) return "zh";
  if (nav.startsWith("ja")) return "ja";
  return "vi";
}

export function saveLang(lang: Lang): void {
  if (typeof window !== "undefined") window.localStorage.setItem(STORAGE_KEY, lang);
}

/** Chuẩn hóa tìm kiếm: thường hóa + bỏ dấu tiếng Việt (pho → Phở) */
export function normalizeSearch(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d");
}
