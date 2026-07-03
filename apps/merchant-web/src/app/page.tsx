"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { getTokens } from "@/lib/api";

export default function Home() {
  const router = useRouter();

  useEffect(() => {
    router.replace(getTokens() ? "/menu" : "/login");
  }, [router]);

  return <main className="p-8 text-gray-500">Đang chuyển hướng...</main>;
}
