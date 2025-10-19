import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const itemsList = document.getElementById("items-list");
const itemTemplate = document.querySelector(".item-card.template");

const prevBtn = document.getElementById("prev-page");
const nextBtn = document.getElementById("next-page");
const pageIndicator = document.getElementById("page-indicator");

const HOME_MESSAGES_ID = "home-messages";
const currentOwnerId =
  window.localStorage.getItem("hippo-owner-id") ??
  window.localStorage.getItem("hippo-user-id");

let items = [];
let currentPage = 1;
const itemsPerPage = 6; // number of cards to show at once

// Load all items from backend
async function loadItems() {
  try {
    const res = await fetch(`${API_BASE}/api/items`);
    if (!res.ok) throw new Error(await res.text() || "Failed to load items.");

    items = await res.json();
    currentPage = 1; // always reset to page 1 when reloading
    renderItems();
  } catch (err) {
    console.error("Error loading items:", err);
    showMessage(
      HOME_MESSAGES_ID,
      err.message || "Unable to load items.",
      "error",
      { autoHide: false }
    );
  }
}

// Render items for the current page
function renderItems() {
  if (!itemsList || !itemTemplate) return;

  // remove old cards except template
  itemsList.querySelectorAll(".item-card:not(.template)").forEach(el => el.remove());

  const totalPages = Math.max(1, Math.ceil(items.length / itemsPerPage));
  const start = (currentPage - 1) * itemsPerPage;
  const end = start + itemsPerPage;
  const currentItems = items.slice(start, end);

  // populate visible cards
  currentItems.forEach(item => {
    const card = itemTemplate.cloneNode(true);
    card.classList.remove("template");
    card.style.display = "";

    const img = card.querySelector(".item-image");
    const name = card.querySelector(".item-name");
    const price = card.querySelector(".item-price");
    const button = card.querySelector(".item-button");

    img.src =
      item.picture?.trim() ||
      "https://via.placeholder.com/320x200?text=Hippo+Exchange";
    img.alt = item.name || "Item";
    name.textContent = item.name || "Unnamed Item";
    price.textContent = `$${Number(item.pricePerDay || 0).toFixed(2)}/day`;

    if (item.isLent) {
      button.textContent = "Borrowed";
      button.disabled = true;
    } else {
      button.textContent = "Request";
      button.disabled = false;
      button.onclick = () => alert(`Request sent for ${item.name}`);
    }

    itemsList.appendChild(card);
  });

  // Update page controls
  updatePagination(totalPages);
}

// Update pagination controls (buttons + page number)
function updatePagination(totalPages) {
  pageIndicator.textContent = `Page ${currentPage} of ${totalPages}`;
  prevBtn.disabled = currentPage === 1;
  nextBtn.disabled = currentPage === totalPages;
}

// Scroll smoothly to the top of the section when changing pages
function scrollToItemsTop() {
  const section = document.getElementById("items");
  if (section) section.scrollIntoView({ behavior: "smooth" });
}

// Button handlers
prevBtn?.addEventListener("click", () => {
  if (currentPage > 1) {
    currentPage--;
    renderItems();
    scrollToItemsTop();
  }
});

nextBtn?.addEventListener("click", () => {
  const totalPages = Math.max(1, Math.ceil(items.length / itemsPerPage));
  if (currentPage < totalPages) {
    currentPage++;
    renderItems();
    scrollToItemsTop();
  }
});

// Initial load
loadItems();
