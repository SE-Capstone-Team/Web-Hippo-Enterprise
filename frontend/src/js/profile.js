import { showMessage } from "./utils.js";

const API_BASE = "https://api.centralspiral.pro";
const MESSAGES_ID = "profile-messages";
const DEFAULT_PFP = "images/BernardDaHippo.png";
const BORROWED_CONTAINER_ID = "borrowed-items";
let ownerId = localStorage.getItem("hippo-owner-id") ?? localStorage.getItem("hippo-user-id");

if (!localStorage.getItem("hippo-owner-id") && ownerId) {
  localStorage.setItem("hippo-owner-id", ownerId);
  localStorage.removeItem("hippo-user-id");
}

document.addEventListener("DOMContentLoaded", () => {
  loadProfile();

  document.getElementById("save-profile")?.addEventListener("click", saveProfile);
  document.getElementById("delete-profile")?.addEventListener("click", deleteProfile);
});

async function loadProfile() {
  if (!ownerId) {
    showInfo("Welcome! Create your profile to get started.");
    setProfileImage();
    renderBorrowedItems([]);
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${ownerId}`);
    if (res.status === 404) {
      showInfo("We couldn\'t find your profile. Create one below.");
      setProfileImage();
      await loadBorrowedItems();
      return;
    }

    if (!res.ok) {
      throw new Error(await res.text() || "Failed to load profile.");
    }

    const profile = await res.json();
    setInputValue("profile-name", `${profile.firstName} ${profile.lastName}`.trim());
    setInputValue("profile-email", profile.email);
    setInputValue("profile-address", profile.address ?? "");
    setInputValue("profile-role", profile.role ?? "owner");
    setInputValue("profile-pfp", profile.pfp ?? "");
    setProfileImage(profile.pfp);
    await loadBorrowedItems();
    clearMessages();
  } catch (err) {
    showError(err.message ?? "Unable to load profile. Check your connection and try again.");
    renderBorrowedItems([], "Unable to load borrowed items right now.");
  }
}

async function saveProfile() {
  const [firstName, ...rest] = getInputValue("profile-name").trim().split(/\s+/);
  const payload = {
    firstName: firstName ?? "",
    lastName: rest.join(" "),
    email: getInputValue("profile-email").trim(),
    address: getInputValue("profile-address").trim(),
    role: getInputValue("profile-role").trim() || "owner",
    pfp: getInputValue("profile-pfp").trim()
  };

  if (!payload.firstName || !payload.email) {
    showError("Name and email are required.");
    return;
  }

  try {
    if (!ownerId) {
      const createRes = await fetch(`${API_BASE}/api/users`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!createRes.ok) {
        throw new Error(await createRes.text() || "Failed to create profile.");
      }
      const created = await createRes.json();
      ownerId = created.ownerId;
      localStorage.setItem("hippo-owner-id", ownerId);
      showSuccess("Profile created!");
      setProfileImage(payload.pfp);
    } else {
      const updateRes = await fetch(`${API_BASE}/api/users/${ownerId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!updateRes.ok) {
        throw new Error(await updateRes.text() || "Failed to update profile.");
      }
      showSuccess("Profile updated!");
      setProfileImage(payload.pfp);
    }

    await loadBorrowedItems();
  } catch (err) {
    showError(err.message ?? "Unable to save profile.");
  }
}

async function deleteProfile() {
  if (!ownerId) {
    showInfo("No profile to delete.");
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${ownerId}`, { method: "DELETE" });
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to delete profile.");
    }

    localStorage.removeItem("hippo-owner-id");
    localStorage.removeItem("hippo-user-id");
    ownerId = null;
    setInputValue("profile-name", "");
    setInputValue("profile-email", "");
    setInputValue("profile-role", "");
    setInputValue("profile-address", "");
    setInputValue("profile-pfp", "");
    setProfileImage();
    renderBorrowedItems([]);
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

function setProfileImage(src) {
  const img = document.getElementById("profile-image");
  if (!img) {
    return;
  }

  const trimmed = (src ?? "").trim();
  img.src = trimmed || DEFAULT_PFP;
  img.alt = trimmed ? "Profile picture" : "Default profile picture";
}

async function loadBorrowedItems() {
  if (!ownerId) {
    renderBorrowedItems([]);
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${ownerId}/borrowing`);
    if (!res.ok) {
      throw new Error(await res.text() || "Unable to load borrowed items.");
    }

    const items = await res.json();
    renderBorrowedItems(items);
  } catch (err) {
    console.error(err);
    renderBorrowedItems([], err.message ?? "Unable to load borrowed items.");
  }
}

function renderBorrowedItems(items, message) {
  const container = document.getElementById(BORROWED_CONTAINER_ID);
  if (!container) {
    return;
  }

  if (message) {
    container.innerHTML = `<p class="empty-state">${message}</p>`;
    return;
  }

  if (!items?.length) {
    container.innerHTML = '<p class="empty-state">You are not borrowing any items right now.</p>';
    return;
  }

  container.innerHTML = items.map(item => {
    const picture = typeof item.picture === "string" && item.picture.trim()
      ? item.picture.trim()
      : "https://via.placeholder.com/320x200?text=Hippo+Exchange";
    const name = (item.name ?? "Item").toString();
    const borrowedOn = formatDateTime(item.borrowedOn);
    const dueAt = formatDateTime(item.dueAt);
    const owner = (item.ownerId ?? "").toString();

    return `
      <article class="mine-card">
        <div class="thumb-wrap">
          <img class="thumb" src="${picture}" alt="${name}" referrerpolicy="no-referrer" loading="lazy">
        </div>
        <div class="mine-body">
          <div class="mine-title">${name}</div>
          <div class="mine-meta subtle">Lender: ${owner || "Unknown"}</div>
          <div class="mine-meta subtle">Borrowed on: ${borrowedOn}</div>
          <div class="mine-meta subtle">Due: ${dueAt}</div>
        </div>
      </article>`;
  }).join("");
}

function formatDateTime(value) {
  if (!value) {
    return "Not set";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Not set";
  }

  return date.toLocaleString();
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
