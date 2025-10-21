// ===============================
// Notifications (Borrow Requests)
// ===============================
const API_BASE = "http://localhost:8000";
const bell = document.getElementById("notification-bell");
const dropdown = document.getElementById("notifications-dropdown");
const badge = document.getElementById("notification-badge");

// Helper: current user ID
function me() {
  return (
    localStorage.getItem("hippo-owner-id") ||
    localStorage.getItem("hippo-user-id")
  );
}

// Toggle dropdown visibility
bell?.addEventListener("click", async (e) => {
  e.stopPropagation();
  const visible = dropdown.style.display === "block";
  dropdown.style.display = visible ? "none" : "block";

  if (!visible) {
    await refreshRequests(); // refresh only when opening
  }
});

// Close dropdown if clicking outside
document.addEventListener("click", (e) => {
  if (!dropdown.contains(e.target) && e.target !== bell) {
    dropdown.style.display = "none";
  }
});

// ===============================
// Refresh requests + badge
// ===============================
async function refreshRequests() {
  const ownerId = me();
  if (!ownerId) {
    dropdown.innerHTML = "<p>Please log in.</p>";
    hideBadge();
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/api/requests/owner/${ownerId}`);
    if (!res.ok) throw new Error(await res.text());
    const list = await res.json();

    // Filter only pending requests
    const pending = list.filter((r) => r.status === "pending");

    // Update badge visibility
    if (pending.length > 0) {
      showBadge();
    } else {
      hideBadge();
    }

    if (!list.length) {
      dropdown.innerHTML = `<p style="padding:8px;">No new requests</p>`;
      return;
    }

    dropdown.innerHTML = "";
    list.forEach((req) => {
      const row = document.createElement("div");
      row.className = "request-row";
      row.innerHTML = `
        <div><strong>${req.itemName}</strong></div>
        <div>Borrower: ${req.borrowerId}</div>
        <div>Return by: ${
          req.dueAt ? new Date(req.dueAt).toLocaleDateString() : "Not set"
        }</div>
        <div class="row-actions">
          <button class="accept" data-id="${req.requestId}">Accept</button>
          <button class="deny" data-id="${req.requestId}">Deny</button>
        </div>
      `;
      dropdown.appendChild(row);
    });

    // Bind Accept / Deny buttons
    dropdown.querySelectorAll(".accept,.deny").forEach((btn) => {
      btn.addEventListener("click", async (ev) => {
        const id = ev.currentTarget.getAttribute("data-id");
        const accepted = ev.currentTarget.classList.contains("accept");
        await respond(id, accepted);
        await refreshRequests();
      });
    });
  } catch (err) {
    console.error("Failed to refresh requests:", err);
    dropdown.innerHTML = `<p style="padding:8px;color:#b91c1c;">Failed to load requests.</p>`;
    hideBadge();
  }
}

// ===============================
// Respond to a request (Accept / Deny)
// ===============================
async function respond(requestId, accepted) {
  try {
    const res = await fetch(`${API_BASE}/api/requests/${requestId}/respond`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ accepted }),
    });

    if (!res.ok) {
      const msg = await res.text();
      alert(msg || "Unable to respond to request.");
      return;
    }

    if (accepted) {
      console.log("Request accepted. Item marked as borrowed.");
    } else {
      console.log("Request denied.");
    }
  } catch (err) {
    console.error("Error responding to request:", err);
    alert("Failed to send response.");
  }
}

// ===============================
// Badge control helpers
// ===============================
function showBadge() {
  if (badge) badge.style.display = "block";
}

function hideBadge() {
  if (badge) badge.style.display = "none";
}

// ===============================
// Auto-load badge on page load
// ===============================
window.addEventListener("DOMContentLoaded", () => {
  refreshRequests(); // check for pending requests immediately
});
