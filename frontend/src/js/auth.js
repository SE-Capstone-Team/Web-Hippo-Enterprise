import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const messagesId = "auth-messages";
const MAX_PROFILE_IMAGE_BYTES = 5 * 1024 * 1024;

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
// Helpers
// ==========================
function clearMessages() {
  const container = document.getElementById(messagesId);
  if (container) container.innerHTML = "";
}

function validateEmail(email) {
  return email.includes("@") && email.includes(".");
}

async function hashPassword(password) {
  const msgUint8 = new TextEncoder().encode(password);
  const hashBuffer = await crypto.subtle.digest("SHA-256", msgUint8);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map(b => b.toString(16).padStart(2, "0")).join("");
}

// Attach native constraint to the registration password field
const regPasswordInput = document.getElementById("reg-password");
const PASSWORD_MSG =
  "Password requires 5 characters, 1 number, and 1 special character.";
const PASSWORD_PATTERN = "^(?=.*[0-9])(?=.*[!@#$%^&*]).{5,}$";

if (regPasswordInput) {
  // Make the browser aware of the rule
  regPasswordInput.setAttribute("pattern", PASSWORD_PATTERN);
  regPasswordInput.setAttribute("title", PASSWORD_MSG);

  // Real-time validation with a tiny debounce so it feels native
  let t;
  const validateNow = () => {
    // Clear any previous custom message before using native pattern
    regPasswordInput.setCustomValidity("");
    // Force browser to show/hide its native tooltip state
    regPasswordInput.checkValidity();
  };

  regPasswordInput.addEventListener("input", () => {
    clearTimeout(t);
    t = setTimeout(() => {
      validateNow();
      if (!regPasswordInput.checkValidity()) {
        regPasswordInput.reportValidity(); // pop tooltip while typing if invalid
      }
    }, 250);
  });

  // Also show tooltip on blur if invalid
  regPasswordInput.addEventListener("blur", () => {
    validateNow();
    if (!regPasswordInput.checkValidity()) {
      regPasswordInput.reportValidity();
    }
  });
}

// ==========================
// Login
// ==========================
async function handleLogin(event) {
  event.preventDefault();
  clearMessages();

  const email = document.getElementById("login-email")?.value.trim();
  const password = document.getElementById("login-password")?.value.trim() ?? "";

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
    if (!res.ok) throw new Error("Unable to connect to the server.");

    const foundUser = await res.json();

    if (!foundUser.password) {
      showMessage(messagesId, "This account does not have a password set. Please re-register.", "error", { autoHide: false });
      return;
    }

    const hashedInput = await hashPassword(password);
    if (hashedInput !== foundUser.password) {
      showMessage(messagesId, "Incorrect password.", "error", { autoHide: false });
      return;
    }

    localStorage.setItem("hippo-owner-id", foundUser.ownerId);
    localStorage.removeItem("hippo-user-id");
    showMessage(messagesId, "Login successful! Redirecting...", "success");
    setTimeout(() => (window.location.href = "home.html"), 700);
  } catch (err) {
    console.error(err);
    showMessage(messagesId, err.message ?? "Unable to log in.", "error", { autoHide: false });
  }
}

// ==========================
// Registration
// ==========================
async function handleRegister(event) {
  event.preventDefault();
  clearMessages();

  const firstName = document.getElementById("reg-first-name")?.value.trim() ?? "";
  const lastName = document.getElementById("reg-last-name")?.value.trim() ?? "";
  const email = document.getElementById("reg-email")?.value.trim() ?? "";
  const role = document.getElementById("reg-role")?.value.trim() || "owner";
  const address = document.getElementById("reg-address")?.value.trim() ?? "";
  const pfpInput = document.getElementById("reg-pfp");
  const pfpFile = pfpInput?.files?.[0] ?? null;

  const passwordInput = regPasswordInput; // reuse
  const password = passwordInput?.value.trim() ?? "";

  if (!firstName || !email || !password || !address) {
    showMessage(messagesId, "First name, email, home address, and password are required.", "error", { autoHide: false });
    return;
  }

  if (!validateEmail(email)) {
    showMessage(messagesId, "Please enter a valid email address.", "error", { autoHide: false });
    return;
  }

  // Let the browser enforce the pattern & show the native tooltip
  if (passwordInput && !passwordInput.checkValidity()) {
    passwordInput.reportValidity(); // shows “Password requires…”
    return;
  }

  let pfpUrl = "";
  if (pfpFile) {
    if (!pfpFile.type.startsWith("image/")) {
      showMessage(messagesId, "Profile photo must be an image file.", "error", { autoHide: false });
      return;
    }
    if (pfpFile.size > MAX_PROFILE_IMAGE_BYTES) {
      showMessage(messagesId, "Profile photo must be smaller than 5MB.", "error", { autoHide: false });
      return;
    }

    const uploadData = new FormData();
    uploadData.append("file", pfpFile);
    if (email) uploadData.append("ownerId", email);

    showMessage(messagesId, "Uploading profile photo...", "info", { autoHide: true, timeout: 1500 });

    const uploadRes = await fetch(`${API_BASE}/api/uploads/profiles`, { method: "POST", body: uploadData });
    if (!uploadRes.ok) throw new Error(await uploadRes.text() || "Unable to upload profile photo.");

    const uploadPayload = await uploadRes.json();
    pfpUrl = (uploadPayload?.url ?? "").toString().trim();
    if (!pfpUrl) throw new Error("Profile photo upload did not return a download URL.");
  }

  const hashedPassword = await hashPassword(password);
  const payload = { firstName, lastName, email, role, address, pfp: pfpUrl, password: hashedPassword };

  try {
    const res = await fetch(`${API_BASE}/api/users`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error(await res.text() || "Registration failed.");

    const created = await res.json();
    localStorage.setItem("hippo-owner-id", created.ownerId);
    localStorage.removeItem("hippo-user-id");
    showMessage(messagesId, "Account created! Redirecting to your profile...", "success");
    if (pfpInput) pfpInput.value = "";
    setTimeout(() => (window.location.href = "profile.html"), 700);
  } catch (err) {
    console.error(err);
    showMessage(messagesId, err.message ?? "Unable to register.", "error", { autoHide: false });
  }
}
