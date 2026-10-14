export type Grant = { role: string; scope: string; departmentId: string | null };
export type Access = { organizationId: string; userId: string; employeeId: string | null; email: string; grants: Grant[]; isAdmin: boolean };
let csrf = "";
export class ApiError extends Error { constructor(public status: number, message: string) { super(message); } }
export async function renewCsrf() {
  const response = await fetch("/api/v1/auth/csrf", { credentials: "same-origin", cache: "no-store" });
  if (!response.ok) throw new ApiError(response.status, "Сервер недоступен");
  csrf = (await response.json()).token;
}
export async function request<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  if (method !== "GET" && !csrf) await renewCsrf();
  const response = await fetch("/api/v1" + path, {
    method, credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", ...(method !== "GET" ? { "X-CSRF-TOKEN": csrf } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.title || (response.status === 401 ? "Войдите в систему" : "Не удалось выполнить запрос"));
  }
  return response.status === 204 ? undefined as T : response.json();
}
