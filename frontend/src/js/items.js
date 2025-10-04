import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const MESSAGE_CONTAINER_ID = "items-messages";

let itemsList;
let addItemForm;
let currentUserId;

document.addEventListener("DOMContentLoaded", () => {
  itemsList = document.getElementById("items-list");
  addItemForm = document.getElementById("add-item-form");
  currentUserId = window.localStorage.getItem("hippo-user-id");

  if (!currentUserId) {
    showMessage(MESSAGE_CONTAINER_ID, "Please log in before managing items.", "error");
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
    const res = await fetch(`${API_BASE}/api/users/${currentUserId}/items`);
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to load items.");
    }

    const items = await res.json();
    renderItems(items);
  } catch (err) {
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to load items.", "error");
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
    const card = document.createElement("article");
    card.className = "mine-card";
    card.innerHTML = `
      <div class="thumb-wrap">
        <img class="thumb" src="${item.picture || "images/placeholder.jpg"}" alt="${item.name}">
        <span class="badge ${String(item.status || "").toLowerCase()}">${item.status ?? ""}</span>
      </div>
      <div class="mine-body">
        <div class="mine-title">${item.name}</div>
        <div class="mine-meta">$${Number(item.pricePerDay || 0).toFixed(2)}/day · ${item.location || "Unknown"}</div>
        <div class="mine-meta subtle">Condition: ${item.condition || "N/A"}</div>
      </div>
      <div class="mine-actions">
        <button class="item-button" data-action="toggle" data-id="${item.itemId}" data-status="${item.status || "Listed"}">
          ${item.status === "Loaned" ? "Mark as Listed" : "Mark as Loaned"}
        </button>
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
  const payload = {
    name: (formData.get("name") ?? "").trim(),
    pricePerDay: Number(formData.get("pricePerDay") ?? 0),
    picture: (formData.get("picture") ?? "").toString().trim(),
    location: (formData.get("location") ?? "").toString().trim(),
    status: (formData.get("status") ?? "Listed").toString(),
    condition: (formData.get("condition") ?? "").toString().trim(),
    ownerUserId: currentUserId
  };

  if (!payload.name) {
    showMessage(MESSAGE_CONTAINER_ID, "Item name is required.", "error");
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
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to add item.", "error");
  }
}

async function handleItemAction(event) {
  const button = event.currentTarget;
  const itemId = button.getAttribute("data-id");
  const action = button.getAttribute("data-action");

  if (!itemId) {
    return;
  }

  if (action === "delete") {
    await deleteItem(itemId);
  } else if (action === "toggle") {
    const currentStatus = button.getAttribute("data-status") || "Listed";
    const nextStatus = currentStatus === "Loaned" ? "Listed" : "Loaned";
    await updateItemStatus(itemId, nextStatus);
  }
}

async function updateItemStatus(itemId, status) {
  try {
    const existingRes = await fetch(`${API_BASE}/api/items/${itemId}`);
    if (!existingRes.ok) {
      throw new Error(await existingRes.text() || "Unable to load item for update.");
    }

    const existing = await existingRes.json();
    const payload = { ...existing, status };

    const res = await fetch(`${API_BASE}/api/items/${itemId}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      throw new Error(await res.text() || "Failed to update item.");
    }

    showMessage(MESSAGE_CONTAINER_ID, "Item status updated.", "success");
    await loadItems();
  } catch (err) {
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to update item.", "error");
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
    showMessage(MESSAGE_CONTAINER_ID, err.message ?? "Unable to delete item.", "error");
  }
}
