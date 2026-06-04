"use client";

import { useEffect, useState } from "react";
import { api, setToken } from "@/lib/api";

export default function LoginPage() {
  const [tokenInput, setTokenInput] = useState("");

  // After GitHub OAuth, the backend can redirect here with #token=... in the fragment.
  useEffect(() => {
    const hash = window.location.hash;
    const prefix = "#token=";
    if (hash.startsWith(prefix)) {
      setToken(decodeURIComponent(hash.slice(prefix.length)));
      window.location.href = "/reviews";
    }
  }, []);

  function useTyped(e: React.FormEvent) {
    e.preventDefault();
    if (!tokenInput.trim()) return;
    setToken(tokenInput.trim());
    window.location.href = "/reviews";
  }

  return (
    <main>
      <h1>Sign in</h1>
      <div className="card">
        <p>Sign in with your GitHub account to review pull requests.</p>
        <a href={`${api.apiUrl}/auth/github/login`}>
          <button className="approve">Sign in with GitHub</button>
        </a>
      </div>

      <div className="card">
        <p className="muted">Developer: paste a token</p>
        <form onSubmit={useTyped} className="row">
          <input
            value={tokenInput}
            onChange={(e) => setTokenInput(e.target.value)}
            placeholder="Paste JWT access token"
          />
          <button className="secondary" type="submit">Use token</button>
        </form>
      </div>
    </main>
  );
}
