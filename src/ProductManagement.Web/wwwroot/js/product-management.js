(() => {
    "use strict";

    const idrFormatter = new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const normalizeCurrency = value => {
        const text = String(value ?? "").trim();
        const integerText = /^Rp\s*/i.test(text) ? text.replace(/^Rp\s*/i, "").replace(/,\d{0,2}$/, "") : text;
        return integerText.replace(/\D/g, "").replace(/^0+(?=\d)/, "");
    };
    const formatCurrency = value => value === "" ? "" : idrFormatter.format(Number(value));
    const currencyKeyResult = (raw, key, replacing = false) => {
        if (/^\d$/.test(key)) return replacing ? key : `${raw}${key}`.replace(/^0+(?=\d)/, "");
        if (key === "Backspace") return replacing ? "" : raw.slice(0, -1);
        if (key === "Delete") return "";
        return raw;
    };
    if (typeof module !== "undefined" && module.exports) module.exports = { normalizeCurrency, formatCurrency, currencyKeyResult };
    if (typeof document === "undefined") return;

    const tokenKey = "productManagementToken";
    const emailKey = "productManagementEmail";
    const elements = Object.fromEntries([
        "toastRegion", "authPanel", "productPanel", "userEmail", "loginForm", "registerForm",
        "loginSubmitButton", "registerSubmitButton", "logoutButton", "addProductButton", "emptyAddButton",
        "filterForm", "clearFiltersButton", "filterName", "minPrice", "maxPrice", "loadingState",
        "emptyState", "productTable", "productRows", "productForm", "productModal", "productModalTitle",
        "productName", "productDescription", "productPrice", "saveProductButton", "deleteModal",
        "deleteProductName", "confirmDeleteButton", "productCount"
    ].map(id => [id, document.getElementById(id)]));

    const productModal = new bootstrap.Modal(elements.productModal);
    const deleteModal = new bootstrap.Modal(elements.deleteModal);
    const noticeDurations = { success: 3500, info: 4000, warning: 5000, error: 6000 };
    let editingProductId = null;
    let pendingDeleteProduct = null;
    let products = [];
    const currencyValues = new WeakMap();

    function setCurrencyValue(input, value) {
        const raw = normalizeCurrency(value);
        currencyValues.set(input, raw);
        input.value = formatCurrency(raw);
    }

    function getCurrencyValue(input) {
        return currencyValues.get(input) ?? normalizeCurrency(input.value);
    }

    function initializeCurrencyInputs() {
        document.querySelectorAll("[data-currency-input]").forEach(input => {
            setCurrencyValue(input, input.value);
            input.addEventListener("keydown", event => {
                if (event.ctrlKey || event.metaKey || event.altKey || !(/^\d$/.test(event.key) || event.key === "Backspace" || event.key === "Delete")) return;
                event.preventDefault();
                const replacing = input.selectionStart !== input.selectionEnd;
                setCurrencyValue(input, currencyKeyResult(getCurrencyValue(input), event.key, replacing));
                input.setSelectionRange(input.value.length, input.value.length);
            });
            input.addEventListener("paste", event => {
                event.preventDefault();
                setCurrencyValue(input, normalizeCurrency(event.clipboardData.getData("text")));
            });
            input.addEventListener("input", () => setCurrencyValue(input, input.value));
        });
    }

    function showToast(type, title, message = "") {
        const toast = document.createElement("article");
        toast.className = `notice-toast notice-${type}`;
        toast.setAttribute("role", type === "error" ? "alert" : "status");

        const indicator = document.createElement("span");
        indicator.className = "notice-indicator";
        indicator.setAttribute("aria-hidden", "true");
        indicator.textContent = type === "success" ? "✓" : type === "error" ? "×" : type === "warning" ? "!" : "i";

        const copy = document.createElement("div");
        const heading = document.createElement("strong");
        heading.className = "notice-title";
        heading.textContent = title;
        copy.append(heading);
        if (message) {
            const detail = document.createElement("p");
            detail.className = "notice-message";
            detail.textContent = message;
            copy.append(detail);
        }

        const close = document.createElement("button");
        close.type = "button";
        close.className = "notice-close";
        close.setAttribute("aria-label", "Dismiss notification");
        close.textContent = "×";
        toast.append(indicator, copy, close);
        elements.toastRegion.append(toast);
        while (elements.toastRegion.children.length > 4) elements.toastRegion.firstElementChild.remove();
        requestAnimationFrame(() => toast.classList.add("is-visible"));

        let remaining = noticeDurations[type] ?? noticeDurations.info;
        let startedAt;
        let timer;
        const dismiss = () => {
            clearTimeout(timer);
            toast.classList.add("is-leaving");
            toast.addEventListener("transitionend", () => toast.remove(), { once: true });
            setTimeout(() => toast.remove(), 300);
        };
        const startTimer = () => { startedAt = Date.now(); timer = setTimeout(dismiss, remaining); };
        const pauseTimer = () => { clearTimeout(timer); remaining -= Date.now() - startedAt; };
        toast.addEventListener("mouseenter", pauseTimer);
        toast.addEventListener("mouseleave", startTimer);
        toast.addEventListener("focusin", pauseTimer);
        toast.addEventListener("focusout", startTimer);
        close.addEventListener("click", dismiss);
        startTimer();
    }

    function setBusy(button, busy, busyText) {
        if (!button.dataset.idleText) button.dataset.idleText = button.textContent;
        button.disabled = busy;
        button.replaceChildren();
        if (busy) {
            const spinner = document.createElement("span");
            spinner.className = "button-spinner";
            spinner.setAttribute("aria-hidden", "true");
            button.append(spinner, document.createTextNode(busyText));
        } else button.textContent = button.dataset.idleText;
    }

    function initializePasswordToggles() {
        document.querySelectorAll("[data-password-toggle]").forEach(button => {
            const input = document.getElementById(button.dataset.passwordToggle);
            button.addEventListener("click", () => {
                const visible = input.type === "text";
                input.type = visible ? "password" : "text";
                button.setAttribute("aria-pressed", String(!visible));
                button.setAttribute("aria-label", visible ? "Show password" : "Hide password");
            });
        });
    }

    function setAuthenticated(authenticated, email = "") {
        elements.authPanel.classList.toggle("d-none", authenticated);
        elements.productPanel.classList.toggle("d-none", !authenticated);
        elements.userEmail.textContent = authenticated ? email : "";
    }

    function clearSession(notify = false) {
        sessionStorage.removeItem(tokenKey);
        sessionStorage.removeItem(emailKey);
        setAuthenticated(false);
        if (notify) showToast("warning", "Session expired", "Please sign in again.");
    }

    async function parseResponse(response) {
        if (response.status === 204) return null;
        const contentType = response.headers.get("content-type") || "";
        return contentType.includes("json") ? response.json() : null;
    }

    function errorMessage(payload, fallback) {
        if (payload?.errors) return Object.values(payload.errors).flat().join(" ");
        if (payload?.detail) return payload.detail;
        return fallback;
    }

    async function apiFetch(url, options = {}, protectedRequest = true) {
        const headers = new Headers(options.headers || {});
        const token = sessionStorage.getItem(tokenKey);
        if (protectedRequest && token) headers.set("Authorization", `Bearer ${token}`);
        if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
        let response;
        try { response = await fetch(url, { ...options, headers }); }
        catch { throw new Error("Check your connection and try again.", { cause: "network" }); }
        const payload = await parseResponse(response);
        if (response.status === 401 && protectedRequest) {
            clearSession(true);
            throw new Error("Session expired", { cause: "session" });
        }
        if (!response.ok) {
            const error = new Error(errorMessage(payload, "The request could not be completed."));
            error.status = response.status;
            error.payload = payload;
            throw error;
        }
        return payload;
    }

    function saveSession(auth) {
        sessionStorage.setItem(tokenKey, auth.token);
        sessionStorage.setItem(emailKey, auth.email);
        setAuthenticated(true, auth.email);
    }

    async function authenticate(endpoint, email, password) {
        const auth = await apiFetch(endpoint, { method: "POST", body: JSON.stringify({ email, password }) }, false);
        saveSession(auth);
        await loadProducts();
    }

    async function loadProducts() {
        elements.loadingState.classList.remove("d-none");
        elements.emptyState.classList.add("d-none");
        elements.productTable.classList.add("d-none");
        const query = new URLSearchParams();
        if (elements.filterName.value.trim()) query.set("name", elements.filterName.value.trim());
        if (getCurrencyValue(elements.minPrice)) query.set("minPrice", Number(getCurrencyValue(elements.minPrice)));
        if (getCurrencyValue(elements.maxPrice)) query.set("maxPrice", Number(getCurrencyValue(elements.maxPrice)));
        try {
            products = await apiFetch(`/api/products${query.size ? `?${query}` : ""}`);
            renderProducts();
        } catch (error) {
            if (error.cause !== "session") showToast("error", "Request failed", error.message);
        } finally { elements.loadingState.classList.add("d-none"); }
    }

    function createCell(text, className = "") {
        const cell = document.createElement("td");
        cell.textContent = text;
        if (className) cell.className = className;
        return cell;
    }

    function actionButton(label, className, action) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = label;
        button.addEventListener("click", action);
        return button;
    }

    function renderProducts() {
        elements.productRows.replaceChildren();
        elements.productCount.textContent = products.length.toLocaleString("id-ID");
        elements.emptyState.classList.toggle("d-none", products.length > 0);
        elements.productTable.classList.toggle("d-none", products.length === 0);
        for (const product of products) {
            const row = document.createElement("tr");
            row.append(createCell(product.name), createCell(product.description, "product-description"));
            row.append(createCell(idrFormatter.format(Number(product.price)), "price-cell"));
            row.append(createCell(new Date(product.createdAt).toLocaleString(), "date-cell"));
            const actions = document.createElement("td");
            actions.className = "table-actions";
            actions.append(
                actionButton("Edit", "btn table-action me-1", () => openProductForm(product)),
                actionButton("Delete", "btn table-action table-action-delete", () => openDeleteConfirmation(product))
            );
            row.append(actions);
            elements.productRows.append(row);
        }
    }

    function openProductForm(product = null) {
        editingProductId = product?.id ?? null;
        elements.productModalTitle.textContent = product ? "Edit product" : "Add product";
        elements.productName.value = product?.name ?? "";
        elements.productDescription.value = product?.description ?? "";
        setCurrencyValue(elements.productPrice, product?.price ?? "");
        productModal.show();
        elements.productModal.addEventListener("shown.bs.modal", () => elements.productName.focus(), { once: true });
    }

    async function saveProduct(event) {
        event.preventDefault();
        const updating = editingProductId !== null;
        setBusy(elements.saveProductButton, true, "Saving…");
        try {
            await apiFetch(updating ? `/api/products/${editingProductId}` : "/api/products", {
                method: updating ? "PUT" : "POST",
                body: JSON.stringify({ name: elements.productName.value, description: elements.productDescription.value, price: Number(getCurrencyValue(elements.productPrice)) })
            });
            productModal.hide();
            elements.productForm.reset();
            editingProductId = null;
            showToast("success", updating ? "Product updated" : "Product created");
            await loadProducts();
        } catch (error) {
            if (error.cause !== "session") showToast(error.status === 400 ? "warning" : "error", error.status === 400 ? "Check the form" : "Couldn't save product", error.message);
        } finally { setBusy(elements.saveProductButton, false); }
    }

    function openDeleteConfirmation(product) {
        pendingDeleteProduct = product;
        elements.deleteProductName.textContent = `“${product.name}”`;
        deleteModal.show();
    }

    async function confirmDelete() {
        if (!pendingDeleteProduct) return;
        setBusy(elements.confirmDeleteButton, true, "Deleting…");
        try {
            await apiFetch(`/api/products/${pendingDeleteProduct.id}`, { method: "DELETE" });
            deleteModal.hide();
            pendingDeleteProduct = null;
            showToast("success", "Product deleted");
            await loadProducts();
        } catch (error) {
            if (error.cause !== "session") showToast("error", "Couldn't delete product", error.message);
        } finally { setBusy(elements.confirmDeleteButton, false); }
    }

    elements.loginForm.addEventListener("submit", async event => {
        event.preventDefault();
        setBusy(elements.loginSubmitButton, true, "Signing in…");
        try {
            await authenticate("/api/auth/login", document.getElementById("loginEmail").value, document.getElementById("loginPassword").value);
            showToast("success", "Signed in", "You are now signed in.");
        } catch (error) {
            showToast("error", error.cause === "network" ? "Request failed" : "Sign in failed", error.cause === "network" ? error.message : "Check your email and password and try again.");
        } finally { setBusy(elements.loginSubmitButton, false); }
    });

    elements.registerForm.addEventListener("submit", async event => {
        event.preventDefault();
        setBusy(elements.registerSubmitButton, true, "Creating account…");
        try {
            await authenticate("/api/auth/register", document.getElementById("registerEmail").value, document.getElementById("registerPassword").value);
            showToast("success", "Account created", "Your account is ready.");
        } catch (error) {
            const duplicate = error.status === 409;
            showToast(duplicate ? "warning" : "error", duplicate ? "Account already exists" : error.status === 400 ? "Check the form" : "Request failed", error.message);
        } finally { setBusy(elements.registerSubmitButton, false); }
    });

    elements.productForm.addEventListener("submit", saveProduct);
    elements.confirmDeleteButton.addEventListener("click", confirmDelete);
    elements.deleteModal.addEventListener("hidden.bs.modal", () => { pendingDeleteProduct = null; });
    elements.filterForm.addEventListener("submit", event => { event.preventDefault(); loadProducts(); });
    elements.clearFiltersButton.addEventListener("click", () => { elements.filterForm.reset(); setCurrencyValue(elements.minPrice, ""); setCurrencyValue(elements.maxPrice, ""); loadProducts(); });
    elements.addProductButton.addEventListener("click", () => openProductForm());
    elements.emptyAddButton.addEventListener("click", () => openProductForm());
    elements.logoutButton.addEventListener("click", () => clearSession());

    async function initialize() {
        initializePasswordToggles();
        initializeCurrencyInputs();
        const token = sessionStorage.getItem(tokenKey);
        if (!token) return setAuthenticated(false);
        try {
            const identity = await apiFetch("/api/auth/me");
            const email = identity.email || sessionStorage.getItem(emailKey) || "";
            sessionStorage.setItem(emailKey, email);
            setAuthenticated(true, email);
            await loadProducts();
        } catch (error) {
            if (error.cause !== "session") clearSession();
        }
    }

    initialize();
})();
