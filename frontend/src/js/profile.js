import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const MESSAGES_ID = "profile-messages";
let userId = localStorage.getItem("hippo-user-id");

document.addEventListener("DOMContentLoaded", () => {
  loadProfile();

  document.getElementById("save-profile")?.addEventListener("click", saveProfile);
  document.getElementById("delete-profile")?.addEventListener("click", deleteProfile);
});

async function loadProfile() {
  if (!userId) {
    showInfo("Welcome! Create your profile to get started.");
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${userId}`);
    if (res.status === 404) {
      showInfo("We couldn\'t find your profile. Create one below.");
      return;
    }

    if (!res.ok) {
      throw new Error(await res.text() || "Failed to load profile.");
    }

    const profile = await res.json();
    setInputValue("profile-name", `${profile.firstName} ${profile.lastName}`.trim());
    setInputValue("profile-email", profile.email);
    setInputValue("profile-role", profile.role);
    clearMessages();
  } catch (err) {
    showError(err.message ?? "Unable to load profile. Check your connection and try again.");
  }
}

async function saveProfile() {
  const [firstName, ...rest] = getInputValue("profile-name").trim().split(/\s+/);
  const payload = {
    firstName: firstName ?? "",
    lastName: rest.join(" "),
    email: getInputValue("profile-email").trim(),
    role: getInputValue("profile-role").trim() || "owner"
  };

  if (!payload.firstName || !payload.email) {
    showError("Name and email are required.");
    return;
  }

  try {
    if (!userId) {
      const createRes = await fetch(`${API_BASE}/api/users`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!createRes.ok) {
        throw new Error(await createRes.text() || "Failed to create profile.");
      }
      const created = await createRes.json();
      userId = created.userId;
      localStorage.setItem("hippo-user-id", userId);
      showSuccess("Profile created!");
    } else {
      const updateRes = await fetch(`${API_BASE}/api/users/${userId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!updateRes.ok) {
        throw new Error(await updateRes.text() || "Failed to update profile.");
      }
      showSuccess("Profile updated!");
    }
  } catch (err) {
    showError(err.message ?? "Unable to save profile.");
  }
}

async function deleteProfile() {
  if (!userId) {
    showInfo("No profile to delete.");
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${userId}`, { method: "DELETE" });
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to delete profile.");
    }

    localStorage.removeItem("hippo-user-id");
    userId = null;
    setInputValue("profile-name", "");
    setInputValue("profile-email", "");
    setInputValue("profile-role", "");
    showSuccess("Profile deleted.");
  } catch (err) {
    showError(err.message ?? "Unable to delete profile.");
  }
}

function showInfo(message) {
  clearMessages();
  showMessage(MESSAGES_ID, message, "info");
}

function showSuccess(message) {
  clearMessages();
  showMessage(MESSAGES_ID, message, "success");
}

function showError(message) {
  clearMessages();
  showMessage(MESSAGES_ID, message, "error", { autoHide: false });
}

function clearMessages() {
  const container = document.getElementById(MESSAGES_ID);
  if (container) {
    container.innerHTML = "";
  }
}

function getInputValue(id) {
  return document.getElementById(id)?.value ?? "";
}

function setInputValue(id, value) {
  const input = document.getElementById(id);
  if (input) {
    input.value = value;
  }
}
