"use client";

import { usePathname, useRouter } from "next/navigation";
import { clearTokens } from "@/lib/api";

const LINKS = [
  { href: "/orders", label: "Order", icon: "📋" },
  { href: "/menu", label: "Menu", icon: "🍽" },
  { href: "/tables", label: "Bàn & QR", icon: "🪑" },
  { href: "/reports", label: "Báo cáo", icon: "📊" },
  { href: "/staff", label: "Nhân viên", icon: "👥" },
  { href: "/settings", label: "Cài đặt", icon: "⚙️" },
];

interface Props {
  venueName?: string;
  right?: React.ReactNode; // slot cho trạng thái real-time, selector quán...
}

/** Header dùng chung — desktop: nav ngang; mobile/tablet: cuộn ngang, chỉ icon+label gọn. */
export default function MerchantNav({ venueName, right }: Props) {
  const pathname = usePathname();
  const router = useRouter();

  return (
    <header className="sticky top-0 z-20 border-b border-brand-100 bg-white">
      <div className="flex items-center gap-3 px-3 py-2 sm:px-6">
        <span className="shrink-0 text-lg font-extrabold text-brand-600 sm:text-xl">TingGo</span>
        {venueName && (
          <span className="hidden max-w-40 truncate text-xs text-gray-500 md:block lg:max-w-none">
            {venueName}
          </span>
        )}
        <div className="ml-auto flex shrink-0 items-center gap-3">
          {right}
          <button
            onClick={async () => {
              clearTokens();
              router.push("/login");
            }}
            className="text-xs text-gray-400 hover:text-gray-600 sm:text-sm"
          >
            Đăng xuất
          </button>
        </div>
      </div>
      <nav className="no-scrollbar flex overflow-x-auto px-1 sm:px-4">
        {LINKS.map((link) => {
          const active = pathname === link.href;
          return (
            <a
              key={link.href}
              href={link.href}
              className={`flex shrink-0 items-center gap-1.5 whitespace-nowrap border-b-2 px-3 py-2.5 text-sm transition-colors ${
                active
                  ? "border-brand-600 font-bold text-brand-600"
                  : "border-transparent text-gray-500 hover:text-brand-600"
              }`}
            >
              <span className="text-base leading-none">{link.icon}</span>
              {link.label}
            </a>
          );
        })}
      </nav>
    </header>
  );
}
