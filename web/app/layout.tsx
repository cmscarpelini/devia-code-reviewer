import type { ReactNode } from "react";
import Link from "next/link";
import "./globals.css";

export const metadata = {
  title: "DevIA Code Reviewer",
  description: "AI-assisted code review dashboard",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <header className="top">
          <Link href="/reviews">
            <strong>DevIA</strong> Code Reviewer
          </Link>
        </header>
        <div className="container">{children}</div>
      </body>
    </html>
  );
}
