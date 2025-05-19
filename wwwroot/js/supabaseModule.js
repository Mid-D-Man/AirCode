// supabaseModule.js - Handles Supabase database operations
export class SupabaseModule {
    constructor(supabaseUrl, supabaseKey) {
        this.supabaseUrl = supabaseUrl;
        this.supabaseKey = supabaseKey;
    }

    async fetchData(endpoint, options = {}) {
        const defaultOptions = {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${this.supabaseKey}`
            }
        };

        const mergedOptions = { ...defaultOptions, ...options };

        if (options.body && typeof options.body !== 'string') {
            mergedOptions.body = JSON.stringify(options.body);
        }

        try {
            const response = await fetch(`${this.supabaseUrl}${endpoint}`, mergedOptions);

            if (!response.ok) {
                throw new Error(`Supabase error: ${response.status} ${response.statusText}`);
            }

            return await response.json();
        } catch (error) {
            console.error('Supabase fetch error:', error);
            throw error;
        }
    }

    // GET operations
    async get(table, queryParams = {}) {
        const queryString = this.buildQueryString(queryParams);
        return this.fetchData(`/rest/v1/${table}?${queryString}`);
    }

    async getById(table, id, queryParams = {}) {
        const queryString = this.buildQueryString({
            ...queryParams,
            id: `eq.${id}`
        });
        return this.fetchData(`/rest/v1/${table}?${queryString}`);
    }

    // POST operations
    async insert(table, data) {
        return this.fetchData(`/rest/v1/${table}`, {
            method: 'POST',
            body: data
        });
    }

    // PUT operations
    async update(table, id, data) {
        return this.fetchData(`/rest/v1/${table}?id=eq.${id}`, {
            method: 'PATCH',
            headers: {
                'Prefer': 'return=representation'
            },
            body: data
        });
    }

    // DELETE operations
    async delete(table, id) {
        return this.fetchData(`/rest/v1/${table}?id=eq.${id}`, {
            method: 'DELETE',
            headers: {
                'Prefer': 'return=representation'
            }
        });
    }

    // Utility method for RPC functions
    async rpc(functionName, params = {}) {
        return this.fetchData(`/rest/v1/rpc/${functionName}`, {
            method: 'POST',
            body: params
        });
    }

    // Build query string from parameters
    buildQueryString(params) {
        return Object.entries(params)
            .map(([key, value]) => {
                if (value.startsWith('eq.') || value.startsWith('gt.') ||
                    value.startsWith('lt.') || value.startsWith('gte.') ||
                    value.startsWith('lte.') || value.startsWith('like.')) {
                    return `${key}=${encodeURIComponent(value)}`;
                }
                return `${key}=eq.${encodeURIComponent(value)}`;
            })
            .join('&');
    }

    // Function execution
    async executeFunction(functionName, params = {}) {
        return this.fetchData(`/functions/v1/${functionName}`, {
            method: 'POST',
            body: params
        });
    }

    // Offline support
    storeOfflineOperation(operation) {
        const offlineOperations = JSON.parse(localStorage.getItem('supabase_offline_operations') || '[]');
        offlineOperations.push({
            timestamp: new Date().toISOString(),
            ...operation
        });
        localStorage.setItem('supabase_offline_operations', JSON.stringify(offlineOperations));
    }

    getOfflineOperations() {
        return JSON.parse(localStorage.getItem('supabase_offline_operations') || '[]');
    }

    clearOfflineOperation(index) {
        const offlineOperations = this.getOfflineOperations();
        offlineOperations.splice(index, 1);
        localStorage.setItem('supabase_offline_operations', JSON.stringify(offlineOperations));
    }

    async syncOfflineOperations() {
        const operations = this.getOfflineOperations();
        const results = [];

        for (let i = 0; i < operations.length; i++) {
            const op = operations[i];
            try {
                let result;
                switch (op.type) {
                    case 'insert':
                        result = await this.insert(op.table, op.data);
                        break;
                    case 'update':
                        result = await this.update(op.table, op.id, op.data);
                        break;
                    case 'delete':
                        result = await this.delete(op.table, op.id);
                        break;
                    default:
                        throw new Error(`Unknown operation type: ${op.type}`);
                }
                results.push({ success: true, operation: op, result });
                this.clearOfflineOperation(i);
                i--; // Adjust index after removal
            } catch (error) {
                results.push({ success: false, operation: op, error: error.message });
            }
        }

        return results;
    }
}

// Initialize and export the module
window.initSupabaseModule = (url, key) => {
    window.supabaseModule = new SupabaseModule(url, key);
    return window.supabaseModule;
};

// Export the module for direct imports
export default SupabaseModule;