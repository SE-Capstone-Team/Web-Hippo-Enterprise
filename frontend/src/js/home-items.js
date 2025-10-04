import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const messagesId = "items-messages";
const list = document.getElementById("items-list");

document.addEventListener("DOMContentLoaded", loadPublicItems);

async function loadPublicItems() {
  if (!list) {
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/items`);
    if (!res.ok) {
      throw new Error(await res.text() || "Failed to load items.");
    }

    const items = await res.json();
    const available = items.filter(item => (item.status ?? "Listed") === "Listed");
    renderItems(available);
  } catch (err) {
    showMessage(messagesId, err.message ?? "Unable to load items.", "error");
  }
}

function renderItems(items) {
  list.innerHTML = "";

  if (!items.length) {
    list.innerHTML = '<li class="empty-state">No items are currently listed. Check back soon!</li>';
    return;
  }

  items.forEach(item => {
    const li = document.createElement("li");
    li.className = "item-card";
    li.innerHTML = `
      <img class="item-image" src="${item.picture || "images/placeholder.jpg"}" alt="${item.name}">
      <h3 class="item-name">${item.name}</h3>
      <p class="item-price">$${Number(item.pricePerDay || 0).toFixed(2)}/day · ${item.location || "Unknown"}</p>
      <p class="item-condition">Condition: ${item.condition || "N/A"}</p>`;
    list.appendChild(li);
  });
}
