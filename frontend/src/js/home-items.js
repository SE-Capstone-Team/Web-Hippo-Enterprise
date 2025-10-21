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
const itemsPerPage = 6; // number of cards to show per page

// ==========================
// Load all items from backend
// ==========================
async function loadItems() {
  try {
    const res = await fetch(`${API_BASE}/api/items`);
    if (!res.ok) throw new Error(await res.text() || "Failed to load items.");

    items = await res.json();
    currentPage = 1;
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

// ==========================
// Render items with pagination
// ==========================
function renderItems() {
  if (!itemsList || !itemTemplate) return;

  // Clear old cards
  itemsList.querySelectorAll(".item-card:not(.template)").forEach(el => el.remove());

  const totalPages = Math.max(1, Math.ceil(items.length / itemsPerPage));
  const start = (currentPage - 1) * itemsPerPage;
  const end = start + itemsPerPage;
  const currentItems = items.slice(start, end);

  // Populate visible cards
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
      button.onclick = () => openRequestModal(item);
    }

    itemsList.appendChild(card);
  });

  updatePagination(totalPages);
}

// ==========================
// Update pagination controls
// ==========================
function updatePagination(totalPages) {
  pageIndicator.textContent = `Page ${currentPage} of ${totalPages}`;
  prevBtn.disabled = currentPage === 1;
  nextBtn.disabled = currentPage === totalPages;
}

// ==========================
// Modal Logic (Borrow Request)
// ==========================
const modal = document.getElementById("request-modal");
const closeModal = document.querySelector(".close-modal");
const sendRequestBtn = document.getElementById("send-request-btn");
const returnDateInput = document.getElementById("return-date");
const modalItemName = document.getElementById("modal-item-name");

let selectedItem = null;

// Open modal for the selected item
function openRequestModal(item) {
  selectedItem = item;
  modalItemName.textContent = `Requesting: ${item.name}`;
  const today = new Date().toISOString().split("T")[0];
  returnDateInput.min = today;
  returnDateInput.value = today;
  modal.style.display = "flex";
}

// Close modal helper
function closeRequestModal() {
  modal.style.display = "none";
  returnDateInput.value = "";
  selectedItem = null;
}

closeModal?.addEventListener("click", closeRequestModal);
window.addEventListener("click", (e) => {
  if (e.target === modal) closeRequestModal();
});

// Handle sending the borrow request
sendRequestBtn?.addEventListener("click", async () => {
  if (!selectedItem) return;

  const dueStr = returnDateInput.value;
  if (!dueStr) {
    alert("Please select a return date.");
    return;
  }

  const borrowerId =
    localStorage.getItem("hippo-owner-id") ||
    localStorage.getItem("hippo-user-id");

  if (!borrowerId) {
    alert("Please log in to request items.");
    return;
  }

  try {
    // Support both item.itemId or item.id, depending on API mapping
    const itemId = selectedItem.itemId || selectedItem.id;
    if (!itemId) {
      console.error("Item missing ID:", selectedItem);
      alert("Unable to identify this item. Try reloading.");
      return;
    }

    const body = {
      itemId,
      borrowerId,
      dueAt: new Date(dueStr).toISOString()
    };

    const res = await fetch(`${API_BASE}/api/requests`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });

    const text = await res.text(); // read response before checking ok
    if (!res.ok) {
      console.error("Backend error:", text);
      throw new Error(text || "Failed to send request.");
    }

    alert(`Request sent for ${selectedItem.name}`);
    closeRequestModal();
  } catch (err) {
    console.error("Request error:", err);
    alert("Unable to send request. Check console for details.");
  }
});

// ==========================
// Scroll to top on pagination
// ==========================
function scrollToItemsTop() {
  const section = document.getElementById("items");
  if (section) section.scrollIntoView({ behavior: "smooth" });
}

// ==========================
// Pagination buttons
// ==========================
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

// ==========================
// Initial load
// ==========================
loadItems();
