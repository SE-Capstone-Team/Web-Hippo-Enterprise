import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const messagesId = "auth-messages";

const loginPanel = document.getElementById("login-panel");
const registerPanel = document.getElementById("register-panel");
const showLoginBtn = document.getElementById("show-login");
const showRegisterBtn = document.getElementById("show-register");
const loginForm = document.getElementById("login-form");
const registerForm = document.getElementById("register-form");

showLoginBtn?.addEventListener("click", () => togglePanel("login"));
showRegisterBtn?.addEventListener("click", () => togglePanel("register"));

loginForm?.addEventListener("submit", handleLogin);
registerForm?.addEventListener("submit", handleRegister);

function togglePanel(kind) {
  const loginActive = kind === "login";
  loginPanel?.classList.toggle("active", loginActive);
  registerPanel?.classList.toggle("active", !loginActive);
  showLoginBtn?.classList.toggle("active", loginActive);
  showRegisterBtn?.classList.toggle("active", !loginActive);
  clearMessages();
}

function clearMessages() {
  const container = document.getElementById(messagesId);
  if (container) {
    container.innerHTML = "";
  }
}

async function handleLogin(event) {
  event.preventDefault();
  clearMessages();

  const email = document.getElementById("login-email")?.value.trim();
  if (!email) {
    showMessage(messagesId, "Email is required to log in.", "error", { autoHide: false });
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users?email=${encodeURIComponent(email)}`);
    if (res.status === 404) {
      showMessage(messagesId, "We couldn't find an account with that email.", "error", { autoHide: false });
      return;
    }

    if (!res.ok) {
      throw new Error(await res.text() || "Login failed.");
    }

    const profile = await res.json();
    localStorage.setItem("hippo-user-id", profile.userId);
    showMessage(messagesId, "Login successful! Redirecting…", "success");
    setTimeout(() => {
      window.location.href = "items.html";
    }, 600);
  } catch (err) {
    showMessage(messagesId, err.message ?? "Unable to log in.", "error", { autoHide: false });
  }
}

async function handleRegister(event) {
  event.preventDefault();
  clearMessages();

  const firstName = document.getElementById("reg-first-name")?.value.trim() ?? "";
  const lastName = document.getElementById("reg-last-name")?.value.trim() ?? "";
  const email = document.getElementById("reg-email")?.value.trim() ?? "";
  const role = document.getElementById("reg-role")?.value.trim() || "owner";

  if (!firstName || !email) {
    showMessage(messagesId, "First name and email are required.", "error", { autoHide: false });
    return;
  }

  const payload = { firstName, lastName, email, role };

  try {
    const res = await fetch(`${API_BASE}/api/users`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      throw new Error(await res.text() || "Registration failed.");
    }

    const created = await res.json();
    localStorage.setItem("hippo-user-id", created.userId);
    showMessage(messagesId, "Account created! Redirecting to your profile…", "success");
    setTimeout(() => {
      window.location.href = "profile.html";
    }, 700);
  } catch (err) {
    showMessage(messagesId, err.message ?? "Unable to register.", "error", { autoHide: false });
  }
}
