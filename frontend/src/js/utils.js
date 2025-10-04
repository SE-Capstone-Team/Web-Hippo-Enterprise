export function showMessage(containerId, text, type = "success", { autoHide = type !== "error", timeout = 4000 } = {}) {
  const container = document.getElementById(containerId);
  if (!container) {
    return;
  }

  const div = document.createElement("div");
  div.className = `alert ${type}`;
  div.innerText = text;

  container.appendChild(div);

  if (autoHide) {
    setTimeout(() => {
      div.remove();
    }, timeout);
  }
}
