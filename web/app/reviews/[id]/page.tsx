"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api, ApiError, getToken, type ReviewDetail } from "@/lib/api";

export default function ReviewDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const [review, setReview] = useState<ReviewDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [justification, setJustification] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!getToken()) {
      router.push("/login");
      return;
    }
    api.getReview(params.id).then(setReview).catch((e) => setError(String(e)));
  }, [params.id, router]);

  async function decide(decision: "Approved" | "Rejected") {
    setError(null);
    if (decision === "Rejected" && !justification.trim()) {
      setError("A justification is required to reject.");
      return;
    }
    setSubmitting(true);
    try {
      await api.recordVerdict(params.id, decision, decision === "Rejected" ? justification.trim() : null);
      router.push("/reviews");
    } catch (e) {
      setError(e instanceof ApiError ? `${e.status}: ${e.message}` : String(e));
    } finally {
      setSubmitting(false);
    }
  }

  if (error && !review) return <main><p className="error">{error}</p></main>;
  if (!review) return <main><p>Loading…</p></main>;

  const decided = review.status === "Approved" || review.status === "Rejected";

  return (
    <main>
      <p className="muted">
        {review.repositoryFullName} · <a href={review.prUrl} target="_blank" rel="noreferrer">PR #{review.prNumber} ↗</a>
      </p>
      <h1>{review.pullRequestTitle}</h1>
      <p className="muted">by {review.authorLogin} · status {review.status}</p>

      <div className="card">
        <h3>Summary</h3>
        <p>{review.summary ?? "No summary."}</p>
      </div>

      <h3>Findings ({review.findings.length})</h3>
      {review.findings.length === 0 && <div className="card">No findings.</div>}
      {review.findings.map((f, i) => (
        <div className="card" key={i}>
          <div className="row" style={{ justifyContent: "space-between" }}>
            <strong>{f.title}</strong>
            <span className={`badge sev-${f.severity}`}>{f.severity}</span>
          </div>
          <p className="muted">
            {f.category} · {f.filePath}{f.line != null ? `:${f.line}` : ""}
          </p>
          <p>{f.description}</p>
          {f.suggestion && <p className="muted">💡 {f.suggestion}</p>}
        </div>
      ))}

      {decided ? (
        <div className="card">
          <strong>Verdict: {review.verdict?.decision ?? review.status}</strong>
          {review.verdict?.justification && <p className="muted">{review.verdict.justification}</p>}
        </div>
      ) : (
        <div className="card">
          <h3>Your verdict</h3>
          <textarea
            rows={3}
            placeholder="Justification (required to reject)"
            value={justification}
            onChange={(e) => setJustification(e.target.value)}
          />
          {error && <p className="error">{error}</p>}
          <div className="actions">
            <button className="approve" disabled={submitting} onClick={() => decide("Approved")}>Approve</button>
            <button className="reject" disabled={submitting} onClick={() => decide("Rejected")}>Reject</button>
          </div>
        </div>
      )}
    </main>
  );
}
