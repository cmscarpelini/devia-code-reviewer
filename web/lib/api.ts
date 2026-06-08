const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

const TOKEN_KEY = "devia_token";

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  window.localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  window.localStorage.removeItem(TOKEN_KEY);
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (response.status === 401) {
    // Access token missing/expired: drop it and send the user back to login
    // instead of surfacing a raw error on the page.
    clearToken();
    if (typeof window !== "undefined" && window.location.pathname !== "/login") {
      window.location.href = "/login";
    }
    throw new ApiError(401, "Session expired. Please sign in again.");
  }

  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export type ReviewStatus =
  | "Pending"
  | "Processing"
  | "AwaitingHumanReview"
  | "Approved"
  | "Rejected"
  | "Failed";

export interface ReviewListItem {
  id: string;
  repositoryFullName: string;
  prNumber: number;
  pullRequestTitle: string;
  authorLogin: string;
  headSha: string;
  status: ReviewStatus;
  riskScore: number | null;
  findingCount: number;
  createdAt: string;
  prUrl: string;
}

export interface Finding {
  severity: "Blocker" | "Major" | "Minor" | "Info";
  category: string;
  filePath: string;
  line: number | null;
  title: string;
  description: string;
  suggestion: string | null;
}

export interface ReviewDetail {
  id: string;
  repositoryFullName: string;
  prNumber: number;
  pullRequestTitle: string;
  authorLogin: string;
  headSha: string;
  status: ReviewStatus;
  summary: string | null;
  riskScore: number | null;
  prUrl: string;
  createdAt: string;
  findings: Finding[];
  verdict: { decision: string; justification: string | null; createdAt: string } | null;
}

export const api = {
  apiUrl: API_URL,
  listReviews: (status: ReviewStatus = "AwaitingHumanReview") =>
    request<ReviewListItem[]>(`/reviews?status=${status}`),
  getReview: (id: string) => request<ReviewDetail>(`/reviews/${id}`),
  recordVerdict: (id: string, decision: "Approved" | "Rejected", justification: string | null) =>
    request<{ verdictId: string; decision: string }>(`/reviews/${id}/verdict`, {
      method: "POST",
      body: JSON.stringify({ decision, justification }),
    }),
};
