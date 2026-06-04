"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, getToken, clearToken, type ReviewListItem } from "@/lib/api";

export default function ReviewsPage() {
  const router = useRouter();
  const [items, setItems] = useState<ReviewListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!getToken()) {
      router.push("/login");
      return;
    }
    api
      .listReviews("AwaitingHumanReview")
      .then(setItems)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [router]);

  function signOut() {
    clearToken();
    router.push("/login");
  }

  return (
    <main>
      <div className="row" style={{ justifyContent: "space-between" }}>
        <h1>Review queue</h1>
        <button className="secondary" onClick={signOut}>Sign out</button>
      </div>
      <p className="muted">Pull requests awaiting your verdict.</p>

      {loading && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}

      {!loading && !error && items.length === 0 && (
        <div className="card">Nothing awaiting review. 🎉</div>
      )}

      {items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Repository</th>
              <th>Pull request</th>
              <th>Author</th>
              <th>Risk</th>
              <th>Findings</th>
            </tr>
          </thead>
          <tbody>
            {items.map((r) => (
              <tr key={r.id}>
                <td>{r.repositoryFullName}</td>
                <td>
                  <Link href={`/reviews/${r.id}`}>
                    #{r.prNumber} {r.pullRequestTitle}
                  </Link>
                </td>
                <td>{r.authorLogin}</td>
                <td className={r.riskScore != null && r.riskScore >= 100 ? "risk-high" : undefined}>
                  {r.riskScore ?? "—"}
                </td>
                <td>{r.findingCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  );
}
