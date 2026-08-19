(() => {
    "use strict";

    const tokenKey = "productManagementToken";
    const emailKey = "productManagementEmail";
    const elements = Object.fromEntries([
        "message", "authPanel", "productPanel", "userEmail", "loginForm", "registerForm",
        "logoutButton", "addProductButton", "emptyAddButton", "filterForm", "clearFiltersButton",
        "filterName", "minPrice", "maxPrice", "loadingState", "emptyState", "productTable",
        "productRows", "productForm", "productModal", "productModalTitle", "productName",
        "productDescription", "productPrice", "saveProductButton"
    ].map(id => [id, document.getElementById(id)]));

    const modal = new bootstrap.Modal(elements.productModal);
    let editingProductId = null;
    let products = [];

    function showMessage(text, type = "success") {
        elements.message.textContent = text;
        elements.message.className = `alert alert-${type}`;
    }

    function clearMessage() {
        elements.message.textContent = "";
        elements.message.className = "alert d-none";
    }

    function setAuthenticated(authenticated, email = "") {
        elements.authPanel.classList.toggle("d-none", authenticated);
        elements.productPanel.classList.toggle("d-none", !authenticated);
        elements.userEmail.textContent = authenticated ? email : "";
    }

    function clearSession(message) {
        sessionStorage.removeItem(tokenKey);
        sessionStorage.removeItem(emailKey);
        setAuthenticated(false);
        if (message) showMessage(message, "warning");
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

        const response = await fetch(url, { ...options, headers });
        const payload = await parseResponse(response);
        if (response.status === 401 && protectedRequest) {
            clearSession("Your session has expired. Please sign in again.");
            throw new Error("Session expired");
        }
        if (!response.ok) throw new Error(errorMessage(payload, "The request could not be completed."));
        return payload;
    }

    function saveSession(auth) {
        sessionStorage.setItem(tokenKey, auth.token);
        sessionStorage.setItem(emailKey, auth.email);
        setAuthenticated(true, auth.email);
    }

    async function authenticate(endpoint, email, password) {
        clearMessage();
        const auth = await apiFetch(endpoint, {
            method: "POST",
            body: JSON.stringify({ email, password })
        }, false);
        saveSession(auth);
        await loadProducts();
    }

    async function loadProducts() {
        elements.loadingState.classList.remove("d-none");
        elements.emptyState.classList.add("d-none");
        elements.productTable.classList.add("d-none");
        const query = new URLSearchParams();
        if (elements.filterName.value.trim()) query.set("name", elements.filterName.value.trim());
        if (elements.minPrice.value) query.set("minPrice", elements.minPrice.value);
        if (elements.maxPrice.value) query.set("maxPrice", elements.maxPrice.value);

        try {
            products = await apiFetch(`/api/products${query.size ? `?${query}` : ""}`);
            renderProducts();
        } catch (error) {
            if (error.message !== "Session expired") showMessage(error.message, "danger");
        } finally {
            elements.loadingState.classList.add("d-none");
        }
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
        elements.emptyState.classList.toggle("d-none", products.length > 0);
        elements.productTable.classList.toggle("d-none", products.length === 0);

        for (const product of products) {
            const row = document.createElement("tr");
            row.append(createCell(product.name), createCell(product.description, "product-description"));
            row.append(createCell(Number(product.price).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })));
            row.append(createCell(new Date(product.createdAt).toLocaleString()));
            const actions = document.createElement("td");
            actions.className = "text-end text-nowrap";
            actions.append(
                actionButton("Edit", "btn btn-sm btn-outline-dark me-2", () => openProductForm(product)),
                actionButton("Delete", "btn btn-sm btn-outline-danger", () => deleteProduct(product))
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
        elements.productPrice.value = product?.price ?? "";
        modal.show();
        elements.productName.focus();
    }

    async function saveProduct(event) {
        event.preventDefault();
        elements.saveProductButton.disabled = true;
        const payload = {
            name: elements.productName.value,
            description: elements.productDescription.value,
            price: Number(elements.productPrice.value)
        };
        try {
            await apiFetch(editingProductId ? `/api/products/${editingProductId}` : "/api/products", {
                method: editingProductId ? "PUT" : "POST",
                body: JSON.stringify(payload)
            });
            modal.hide();
            elements.productForm.reset();
            showMessage(editingProductId ? "Product updated." : "Product created.");
            editingProductId = null;
            await loadProducts();
        } catch (error) {
            if (error.message !== "Session expired") showMessage(error.message, "danger");
        } finally {
            elements.saveProductButton.disabled = false;
        }
    }

    async function deleteProduct(product) {
        if (!window.confirm(`Delete ${product.name}?`)) return;
        try {
            await apiFetch(`/api/products/${product.id}`, { method: "DELETE" });
            showMessage("Product deleted.");
            await loadProducts();
        } catch (error) {
            if (error.message !== "Session expired") showMessage(error.message, "danger");
        }
    }

    elements.loginForm.addEventListener("submit", async event => {
        event.preventDefault();
        try { await authenticate("/api/auth/login", document.getElementById("loginEmail").value, document.getElementById("loginPassword").value); }
        catch (error) { showMessage(errorMessage(null, error.message === "The request could not be completed." ? "Invalid email or password." : error.message), "danger"); }
    });

    elements.registerForm.addEventListener("submit", async event => {
        event.preventDefault();
        try { await authenticate("/api/auth/register", document.getElementById("registerEmail").value, document.getElementById("registerPassword").value); }
        catch (error) { showMessage(error.message, "danger"); }
    });

    elements.productForm.addEventListener("submit", saveProduct);
    elements.filterForm.addEventListener("submit", event => { event.preventDefault(); loadProducts(); });
    elements.clearFiltersButton.addEventListener("click", () => { elements.filterForm.reset(); loadProducts(); });
    elements.addProductButton.addEventListener("click", () => openProductForm());
    elements.emptyAddButton.addEventListener("click", () => openProductForm());
    elements.logoutButton.addEventListener("click", () => { clearMessage(); clearSession(); });

    async function initialize() {
        const token = sessionStorage.getItem(tokenKey);
        if (!token) return setAuthenticated(false);
        try {
            const identity = await apiFetch("/api/auth/me");
            const email = identity.email || sessionStorage.getItem(emailKey) || "";
            sessionStorage.setItem(emailKey, email);
            setAuthenticated(true, email);
            await loadProducts();
        } catch (error) {
            if (error.message !== "Session expired") clearSession("Please sign in again.");
        }
    }

    initialize();
})();
