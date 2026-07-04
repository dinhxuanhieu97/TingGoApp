"use client";

import { useEffect, useRef, useState } from "react";
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

/** Header dùng chung — desktop: nav ngang; mobile/tablet: cuộn ngang, icon+label gọn. */
export default function MerchantNav({ venueName, right }: Props) {
  const pathname = usePathname();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Đóng menu khi bấm ra ngoài
  useEffect(() => {
    if (!menuOpen) return;
    function onClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, [menuOpen]);

  function logout() {
    clearTokens();
    router.push("/login");
  }

  return (
    <header className="sticky top-0 z-20 border-b border-brand-100 bg-white shadow-sm">
      <div className="flex items-center gap-3 px-3 py-2 sm:px-6">
        <a href="/orders" className="flex shrink-0 items-baseline gap-2">
          <span className="text-lg font-extrabold text-brand-600 sm:text-xl">TingGo</span>
          {venueName && (
            <span className="hidden max-w-40 truncate text-xs text-gray-500 md:inline lg:max-w-none">
              {venueName}
            </span>
          )}
        </a>
        <div className="ml-auto flex shrink-0 items-center gap-3">
          {right}
          {/* Menu người dùng */}
          <div className="relative" ref={menuRef}>
            <button
              onClick={() => setMenuOpen((v) => !v)}
              aria-label="Tài khoản"
              aria-expanded={menuOpen}
              className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-100 text-brand-700 transition-colors hover:bg-brand-200"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none"
                stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </button>
            {menuOpen && (
              <div className="absolute right-0 top-11 w-52 overflow-hidden rounded-xl border border-gray-100 bg-white shadow-lg">
                {venueName && (
                  <div className="border-b border-gray-100 px-4 py-3">
                    <p className="text-[11px] uppercase tracking-wide text-gray-400">Quán</p>
                    <p className="truncate text-sm font-semibold">{venueName}</p>
                  </div>
                )}
                <button
                  onClick={logout}
                  className="flex w-full items-center gap-2 px-4 py-3 text-left text-sm text-danger hover:bg-red-50"
                >
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none"
                    stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                    <polyline points="16 17 21 12 16 7" />
                    <line x1="21" y1="12" x2="9" y2="12" />
                  </svg>
                  Đăng xuất
                </button>
              </div>
            )}
          </div>
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
