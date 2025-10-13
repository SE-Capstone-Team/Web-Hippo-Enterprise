import { showMessage } from "./utils.js";

const API_BASE = "http://localhost:8000";
const itemsList = document.getElementById("items-list");
const prevBtn = document.getElementById("prev-page");
const nextBtn = document.getElementById("next-page");
const pageIndicator = document.getElementById("page-indicator");
const scrollLeft = document.getElementById("scroll-left");
const scrollRight = document.getElementById("scroll-right");
const HOME_MESSAGES_ID = "home-messages";

const currentOwnerId = window.localStorage.getItem("hippo-owner-id") ?? window.localStorage.getItem("hippo-user-id");

let items = [];
let currentPage = 1;
const itemsPerPage = 6;

// Load all items from backend
async function loadItems() {
  try {
    const res = await fetch(`${API_BASE}/api/items`);
    if (!res.ok) throw new Error(await res.text() || "Failed to load items.");

    items = await res.json();
    renderItems();
  } catch (err) {
    console.error("Error loading items:", err);
    showMessage(HOME_MESSAGES_ID, err.message || "Unable to load items.", "error", { autoHide: false });
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
      .map(createItemCard)
      .join("");

    itemsList.style.opacity = 1; // Fade in
  }, 200);

  updatePaginationButtons();
  updateScrollButtons();
}

function createItemCard(item) {
  const picture = typeof item.picture === "string" && item.picture.trim()
    ? item.picture.trim()
    : "https://via.placeholder.com/320x200?text=Hippo+Exchange";
  const name = (item.name ?? "Item").toString();
  const condition = item.condition || "Good";
  const price = Number(item.pricePerDay || 0);
  const isBorrowed = Boolean(item.isLent);
  const borrowedByUser = Boolean(currentOwnerId && item.borrowerId === currentOwnerId);
  const borrowedOnText = formatDateTime(item.borrowedOn);
  const dueAtText = formatDateTime(item.dueAt);

  let actionMarkup;
  if (!currentOwnerId) {
    actionMarkup = '<button class="borrow-btn" type="button" disabled>Log in to borrow</button>';
  } else if (!isBorrowed) {
    actionMarkup = `<button class="borrow-btn" type="button" data-action="borrow" data-id="${item.itemId}">Borrow</button>`;
  } else if (borrowedByUser) {
    actionMarkup = `<button class="borrow-btn" type="button" data-action="return" data-id="${item.itemId}">Return</button>`;
  } else {
    actionMarkup = '<button class="borrow-btn" type="button" disabled>Borrowed</button>';
  }

  const statusText = isBorrowed ? "Loaned" : "Available";

  return `
    <div class="item-card">
      <img src="${picture}" alt="${name}" referrerpolicy="no-referrer" loading="lazy" />
      <h3>${name}</h3>
      <p><strong>Price/Day:</strong> $${price.toFixed(2)}</p>
      <p><strong>Condition:</strong> ${condition}</p>
      <p><strong>Status:</strong> ${statusText}</p>
      <p><strong>Borrowed On:</strong> ${borrowedOnText}</p>
      <p><strong>Due:</strong> ${dueAtText}</p>
      ${actionMarkup}
    </div>`;
}

// Update pagination buttons
function updatePaginationButtons() {
  const totalPages = Math.max(1, Math.ceil(items.length / itemsPerPage));
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
  const totalPages = Math.max(1, Math.ceil(items.length / itemsPerPage));
  if (currentPage < totalPages) {
    currentPage++;
    renderItems();
  }
});

itemsList?.addEventListener("click", event => {
  const button = event.target.closest("button[data-action]");
  if (!button) {
    return;
  }

  const action = button.getAttribute("data-action");
  const itemId = button.getAttribute("data-id");
  if (!itemId || !action) {
    return;
  }

  if (action === "borrow") {
    borrowItem(itemId);
  } else if (action === "return") {
    returnItem(itemId);
  }
});

async function borrowItem(itemId) {
  if (!currentOwnerId) {
    showMessage(HOME_MESSAGES_ID, "Please log in before borrowing items.", "error");
    return;
  }

  const dueAtInput = window.prompt("Enter the due date (YYYY-MM-DD or ISO 8601). Leave blank if undecided.");
  let dueAt = null;
  if (dueAtInput) {
    const dueDate = new Date(dueAtInput);
    if (Number.isNaN(dueDate.getTime())) {
      showMessage(HOME_MESSAGES_ID, "Please enter a valid due date.", "error");
      return;
    }
    dueAt = dueDate.toISOString();
  }

  try {
    const res = await fetch(`${API_BASE}/api/items/${itemId}/borrow`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ borrowerId: currentOwnerId, dueAt })
    });

    if (!res.ok) {
      throw new Error(await res.text() || "Unable to borrow item.");
    }

    showMessage(HOME_MESSAGES_ID, "Item borrowed! Enjoy.", "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(HOME_MESSAGES_ID, err.message ?? "Unable to borrow item.", "error", { autoHide: false });
  }
}

async function returnItem(itemId) {
  try {
    const res = await fetch(`${API_BASE}/api/items/${itemId}/return`, { method: "POST" });
    if (!res.ok) {
      throw new Error(await res.text() || "Unable to return item.");
    }

    showMessage(HOME_MESSAGES_ID, "Thanks for returning the item!", "success");
    await loadItems();
  } catch (err) {
    console.error(err);
    showMessage(HOME_MESSAGES_ID, err.message ?? "Unable to return item.", "error", { autoHide: false });
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

// Initial load
loadItems();
