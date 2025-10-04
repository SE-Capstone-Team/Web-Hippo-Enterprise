import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const itemsList = document.getElementById("items-list");
const prevBtn = document.getElementById("prev-page");
const nextBtn = document.getElementById("next-page");
const pageIndicator = document.getElementById("page-indicator");
const scrollLeft = document.getElementById("scroll-left");
const scrollRight = document.getElementById("scroll-right");

let items = [];
let currentPage = 1;
const itemsPerPage = 6;

// Load all items from backend
async function loadItems() {
  try {
    const res = await fetch(`${API_BASE}/api/items`);
    if (!res.ok) throw new Error("Failed to load items");

    items = await res.json();
    renderItems();
  } catch (err) {
    console.error("Error loading items:", err);
    showMessage("auth-messages", err.message || "Unable to load items", "error", { autoHide: false });
  }
}

// Render items for current page
function renderItems() {
  if (!itemsList) return;

  const start = (currentPage - 1) * itemsPerPage;
  const end = start + itemsPerPage;
  const currentItems = items.slice(start, end);

  itemsList.style.opacity = 0; // Fade out

  setTimeout(() => {
    itemsList.innerHTML = currentItems
      .map(
        (item) => `
        <div class="item-card">
          <img src="${item.picture || 'images/placeholder.png'}" alt="${item.name}" />
          <h3>${item.name}</h3>
          <p><strong>Price/Day:</strong> $${item.pricePerDay || 0}</p>
          <p><strong>Condition:</strong> ${item.condition || "Good"}</p>
          <p><strong>Status:</strong> ${item.status || "Listed"}</p>
        </div>
      `
      )
      .join("");

    itemsList.style.opacity = 1; // Fade in
  }, 200);

  updatePaginationButtons();
  updateScrollButtons();
}

// Update pagination buttons
function updatePaginationButtons() {
  const totalPages = Math.ceil(items.length / itemsPerPage);
  prevBtn.disabled = currentPage === 1;
  nextBtn.disabled = currentPage === totalPages;
  pageIndicator.textContent = `Page ${currentPage} of ${totalPages}`;
}

// Update visibility of scroll arrows
function updateScrollButtons() {
  const totalItems = items.length;
  const visibleItems = Math.min(itemsPerPage, totalItems);
  if (totalItems <= visibleItems) {
    scrollLeft.style.display = "none";
    scrollRight.style.display = "none";
  } else {
    scrollLeft.style.display = "block";
    scrollRight.style.display = "block";
  }
}

// Scroll arrow logic
scrollLeft?.addEventListener("click", () => {
  itemsList.scrollBy({ left: -400, behavior: "smooth" });
});

scrollRight?.addEventListener("click", () => {
  itemsList.scrollBy({ left: 400, behavior: "smooth" });
});

// Pagination navigation
prevBtn?.addEventListener("click", () => {
  if (currentPage > 1) {
    currentPage--;
    renderItems();
  }
});

nextBtn?.addEventListener("click", () => {
  const totalPages = Math.ceil(items.length / itemsPerPage);
    if (currentPage < totalPages) {
    currentPage++;
    renderItems();
  }
});

// Initial load
loadItems();
