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

// ==========================
// Panel Switching
// ==========================
function togglePanel(kind) {
  const loginActive = kind === "login";
  loginPanel?.classList.toggle("active", loginActive);
  registerPanel?.classList.toggle("active", !loginActive);
  showLoginBtn?.classList.toggle("active", loginActive);
  showRegisterBtn?.classList.toggle("active", !loginActive);
  clearMessages();
}

// ==========================
// Helper Functions
// ==========================
function clearMessages() {
  const container = document.getElementById(messagesId);
  if (container) container.innerHTML = "";
}

function validateEmail(email) {
  return email.includes("@") && email.includes(".");
}

// ==========================
// Handle Login
// ==========================
async function handleLogin(event) {
  event.preventDefault();
  clearMessages();

  const email = document.getElementById("login-email")?.value.trim();
  const password = document.getElementById("login-password")?.value.trim();

  // --- Frontend Validation ---
  if (!email || !password) {
    showMessage(messagesId, "Please fill in both email and password.", "error", { autoHide: false });
    return;
  }

  if (!validateEmail(email)) {
    showMessage(messagesId, "Please enter a valid email address.", "error", { autoHide: false });
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users?email=${encodeURIComponent(email)}`);
    if (res.status === 404) {
      showMessage(messagesId, "No user found with that email.", "error", { autoHide: false });
      return;
    }

    if (!res.ok) {
      throw new Error("Unable to connect to the server.");
    }

    const foundUser = await res.json();

    // For now, any password works (until Authentik integration)
    localStorage.setItem("hippo-owner-id", foundUser.ownerId);
    localStorage.removeItem("hippo-user-id");
    showMessage(messagesId, "Login successful! Redirecting...", "success");
    setTimeout(() => {
      window.location.href = "home.html";
    }, 700);
  } catch (err) {
    console.error(err);
    showMessage(messagesId, err.message ?? "Unable to log in.", "error", { autoHide: false });
  }
}

// ==========================
// Handle Registration
// ==========================
async function handleRegister(event) {
  event.preventDefault();
  clearMessages();

  const firstName = document.getElementById("reg-first-name")?.value.trim() ?? "";
  const lastName = document.getElementById("reg-last-name")?.value.trim() ?? "";
  const email = document.getElementById("reg-email")?.value.trim() ?? "";
  const role = document.getElementById("reg-role")?.value.trim() || "owner";
  const address = document.getElementById("reg-address")?.value.trim() ?? "";
  const pfp = document.getElementById("reg-pfp")?.value.trim() ?? "";
  const password = document.getElementById("reg-password")?.value.trim() ?? "";

  // --- Frontend Validation ---
  if (!firstName || !email || !password || !address) {
    showMessage(messagesId, "First name, email, home address, and password are required.", "error", { autoHide: false });
    return;
  }

  if (!validateEmail(email)) {
    showMessage(messagesId, "Please enter a valid email address.", "error", { autoHide: false });
    return;
  }

  const payload = { firstName, lastName, email, role, address, pfp };

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
    localStorage.setItem("hippo-owner-id", created.ownerId);
    localStorage.removeItem("hippo-user-id");
    showMessage(messagesId, "Account created! Redirecting to your profile...", "success");
    setTimeout(() => {
      window.location.href = "profile.html";
    }, 700);
  } catch (err) {
    console.error(err);
    showMessage(messagesId, err.message ?? "Unable to register.", "error", { autoHide: false });
  }
}
