import { showMessage } from "./utils.js";

const API_BASE = "https://api.centralspiral.pro";
const MESSAGE_CONTAINER_ID = "items-messages";

let itemsList;
let addItemForm;
let currentOwnerId;

document.addEventListener("DOMContentLoaded", () => {
  itemsList = document.getElementById("items-list");
  addItemForm = document.getElementById("add-item-form");
  currentOwnerId = window.localStorage.getItem("hippo-owner-id");

  if (!currentOwnerId) {
    const legacyId = window.localStorage.getItem("hippo-user-id");
    if (legacyId) {
      currentOwnerId = legacyId;
      window.localStorage.setItem("hippo-owner-id", legacyId);
      window.localStorage.removeItem("hippo-user-id");
    }
  }

  if (!currentOwnerId) {
    showMessage(MESSAGE_CONTAINER_ID, "Please log in before managing items.", "error", { autoHide: false });
    disableForm();
    return;
  }

  loadItems();
  addItemForm?.addEventListener("submit", handleAddItem);
});

function disableForm() {
  if (!addItemForm) {
    return;
  }

  addItemForm.querySelectorAll("input, select, button").forEach(el => el.disabled = true);
  addItemForm.setAttribute("aria-disabled", "true");
}

async function loadItems() {
  if (!itemsList) {
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/users/${currentOwnerId}/items`);
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to load items.");
    }

    const items = await res.json();
    renderItems(items);
  } catch (err) {
    console.error(err);
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to load items.", "error", { autoHide: false });
  }
}

function renderItems(items) {
  if (!itemsList) {
    return;
  }

  itemsList.innerHTML = "";

  if (!items?.length) {
    itemsList.innerHTML = '<p class="empty-state">No items yet. Add your first listing above.</p>';
    return;
  }

  items.forEach(item => {
    const isLent = Boolean(item?.isLent);
    const borrowerId = (item?.borrowerId ?? "").trim();
    const borrowedOn = formatDateTime(item?.borrowedOn);
    const dueAt = formatDateTime(item?.dueAt);
    const statusLabel = isLent ? "Loaned" : "Listed";
    const statusClass = isLent ? "loaned" : "listed";
    const pictureSrc = typeof item.picture === "string" && item.picture.trim()
      ? item.picture.trim()
      : "https://via.placeholder.com/320x200?text=Hippo+Exchange";
    const itemName = (item?.name ?? "Item").toString();

    const borrowerDetails = isLent
      ? `
        <div class="mine-meta subtle">Borrower ID: ${borrowerId || "Unknown"}</div>
        <div class="mine-meta subtle">Borrowed on: ${borrowedOn}</div>
        <div class="mine-meta subtle">Due: ${dueAt}</div>`
      : '<div class="mine-meta subtle">Borrower: —</div>';

    const primaryAction = isLent
      ? `<button class="item-button" data-action="return" data-id="${item.itemId}">Mark as Returned</button>`
      : `<button class="item-button" data-action="loan" data-id="${item.itemId}">Loan Item</button>`;

    const card = document.createElement("article");
    card.className = "mine-card";
    card.innerHTML = `
      <div class="thumb-wrap">
        <img class="thumb" src="${pictureSrc}" alt="${itemName}" referrerpolicy="no-referrer" loading="lazy">
        <span class="badge ${statusClass}">${statusLabel}</span>
      </div>
      <div class="mine-body">
        <div class="mine-title">${itemName}</div>
        <div class="mine-meta">$${Number(item.pricePerDay || 0).toFixed(2)}/day - ${item.location || "Unknown"}</div>
        <div class="mine-meta subtle">Condition: ${item.condition || "N/A"}</div>
        ${borrowerDetails}
      </div>
      <div class="mine-actions">
        ${primaryAction}
        <button class="item-button delete" data-action="delete" data-id="${item.itemId}">Delete</button>
      </div>`;

    itemsList.appendChild(card);
  });

  itemsList.querySelectorAll("button[data-action]").forEach(button =>
    button.addEventListener("click", handleItemAction)
  );
}

async function handleAddItem(event) {
  event.preventDefault();
  if (!addItemForm) {
    return;
  }

  const formData = new FormData(addItemForm);
  const isLentValue = (formData.get("isLent") ?? "false").toString();
  const payload = {
    name: (formData.get("name") ?? "").trim(),
    pricePerDay: Number(formData.get("pricePerDay") ?? 0),
    picture: (formData.get("picture") ?? "").toString().trim(),
    location: (formData.get("location") ?? "").toString().trim(),
    condition: (formData.get("condition") ?? "").toString().trim(),
    isLent: isLentValue === "true",
    ownerId: currentOwnerId
  };

  if (!payload.name) {
    showMessage(MESSAGE_CONTAINER_ID, "Item name is required.", "error");
    return;
  }

  if (!Number.isFinite(payload.pricePerDay) || payload.pricePerDay < 0) {
    showMessage(MESSAGE_CONTAINER_ID, "Price per day must be zero or greater.", "error");
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/items`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      throw new Error(await res.text() || "Failed to add item.");
    }

    addItemForm.reset();
    showMessage(MESSAGE_CONTAINER_ID, `Added "${payload.name}".`, "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to add item.", "error", { autoHide: false });
  }
}

async function handleItemAction(event) {
  const button = event.currentTarget;
  const itemId = button.getAttribute("data-id");
  const action = button.getAttribute("data-action");

  if (!itemId || !action) {
    return;
  }

  if (action === "delete") {
    await deleteItem(itemId);
  } else if (action === "loan") {
    await loanItem(itemId);
  } else if (action === "return") {
    await returnItem(itemId);
  }
}

async function loanItem(itemId) {
  const borrowerIdInput = window.prompt("Enter the borrower's owner ID:");
  if (!borrowerIdInput) {
    showMessage(MESSAGE_CONTAINER_ID, "Borrower ID is required to loan an item.", "error");
    return;
  }

  const borrowerId = borrowerIdInput.trim();
  if (!borrowerId) {
    showMessage(MESSAGE_CONTAINER_ID, "Borrower ID cannot be blank.", "error");
    return;
  }

  const dueAtInput = window.prompt("Enter the due date (YYYY-MM-DD or ISO 8601). Leave blank if undecided.");
  let dueAt = null;
  if (dueAtInput) {
    const dueDate = new Date(dueAtInput);
    if (Number.isNaN(dueDate.getTime())) {
      showMessage(MESSAGE_CONTAINER_ID, "Please enter a valid due date.", "error");
      return;
    }
    dueAt = dueDate.toISOString();
  }

  try {
    const res = await fetch(`${API_BASE}/api/items/${itemId}/borrow`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ borrowerId, dueAt })
    });

    if (!res.ok) {
      throw new Error(await res.text() || "Unable to loan item.");
    }

    showMessage(MESSAGE_CONTAINER_ID, "Item loaned.", "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to loan item.", "error", { autoHide: false });
  }
}

async function returnItem(itemId) {
  try {
    const res = await fetch(`${API_BASE}/api/items/${itemId}/return`, { method: "POST" });
    if (!res.ok) {
      throw new Error(await res.text() || "Unable to mark item as returned.");
    }

    showMessage(MESSAGE_CONTAINER_ID, "Item marked as returned.", "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to mark item as returned.", "error", { autoHide: false });
  }
}

async function deleteItem(itemId) {
  try {
    const res = await fetch(`${API_BASE}/api/items/${itemId}`, { method: "DELETE" });
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to delete item.");
    }

    showMessage(MESSAGE_CONTAINER_ID, "Item deleted.", "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to delete item.", "error", { autoHide: false });
  }
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
