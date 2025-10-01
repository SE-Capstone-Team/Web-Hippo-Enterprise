// items.js
import { showMessage } from "./utils.js";

const itemsList = document.getElementById("items-list");
const messagesContainer = "items-messages";

// Load items when page loads
document.addEventListener("DOMContentLoaded", () => {
  if (itemsList) {
    loadItems();
  }

  // Hook into add-item form (items.html)
  const addItemForm = document.getElementById("add-item-form");
  if (addItemForm) {
    addItemForm.addEventListener("submit", handleAddItem);
  }
});

// Fetch and display items
async function loadItems() {
  try {
    const res = await fetch("/api/items");
    if (!res.ok) throw new Error("Failed to load items");

    const items = await res.json();
    renderItems(items);
  } catch (err) {
    showMessage(messagesContainer, "Error loading items: " + err.message, "error");
  }
}

function renderItems(items) {
  itemsList.innerHTML = "";

  if (!items.length) {
    itemsList.innerHTML = "<p>No items available.</p>";
    return;
  }

  items.forEach(item => {
    const li = document.createElement("li");
    li.className = "item-card";
    li.innerHTML = `
      <img class="item-image" src="${item.imageUrl || 'images/download.jpg'}" alt="${item.name}">
      <h3 class="item-name">${item.name}</h3>
      <p class="item-price">$${item.price || 0}/day</p>
      <span class="badge ${item.statusOfBorrow === 'Loaned' ? 'loaned' : 'listed'}">
        ${item.statusOfBorrow || 'Listed'}
      </span>
      <button class="item-button" data-id="${item.itemId}" data-action="request">Request</button>
      <button class="item-button delete" data-id="${item.itemId}" data-action="delete">Delete</button>
    `;

    itemsList.appendChild(li);
  });

  itemsList.querySelectorAll("button").forEach(btn => {
    btn.addEventListener("click", handleItemAction);
  });
}

// Handle item request/delete
async function handleItemAction(e) {
  const id = e.target.getAttribute("data-id");
  const action = e.target.getAttribute("data-action");

  if (action === "request") {
    await updateItemStatus(id, "Loaned");
  } else if (action === "delete") {
    await deleteItem(id);
  }
}

async function updateItemStatus(itemId, status) {
  try {
    const res = await fetch(`/api/items/${itemId}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ statusOfBorrow: status })
    });

    if (!res.ok) throw new Error("Failed to update item");
    showMessage(messagesContainer, "Item status updated!", "success");
    loadItems();
  } catch (err) {
    showMessage(messagesContainer, err.message, "error");
  }
}

async function deleteItem(itemId) {
  try {
    const res = await fetch(`/api/items/${itemId}`, { method: "DELETE" });
    if (!res.ok) throw new Error("Failed to delete item");

    showMessage(messagesContainer, "Item deleted!", "success");
    loadItems();
  } catch (err) {
    showMessage(messagesContainer, err.message, "error");
  }
}

// Add new item (items.html only)
async function handleAddItem(e) {
  e.preventDefault();
  const form = e.target;

  const data = {
    name: form.name.value,
    description: form.description.value,
    price: form.price.value,
    condition: form.condition.value,
    statusOfBorrow: "Listed"
  };

  try {
    const res = await fetch("/api/items", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data)
    });

    if (!res.ok) throw new Error("Failed to add item");

    showMessage(messagesContainer, "Item added successfully!", "success");
    form.reset();
    loadItems();
  } catch (err) {
    showMessage(messagesContainer, err.message, "error");
  }
}
