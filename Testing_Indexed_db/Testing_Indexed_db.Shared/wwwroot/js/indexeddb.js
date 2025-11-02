// IndexedDB Service for Employee Management
// This service works OFFLINE - all data is stored locally in the browser
window.indexedDBService = {
    dbName: 'EmployeeDB',
    dbVersion: 2,
    storeName: 'employees',
    syncActionsStoreName: 'syncActions',
    db: null,
    
    // Check if browser supports IndexedDB
    isSupported() {
        return typeof indexedDB !== 'undefined';
    },
    
    // Check online/offline status
    isOnline() {
        return navigator.onLine;
    },

    // Initialize the database - WORKS OFFLINE
    async init() {
        if (!this.isSupported()) {
            throw new Error('IndexedDB is not supported in this browser');
        }
        
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.dbVersion);

            request.onerror = (event) => {
                console.error('IndexedDB error:', event);
                reject(new Error('Failed to open database'));
            };
            
            request.onblocked = () => {
                console.warn('IndexedDB upgrade blocked');
            };

            request.onsuccess = (event) => {
                this.db = event.target.result;
                resolve(this.db);
            };

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                
                // Create employees object store if it doesn't exist
                if (!db.objectStoreNames.contains(this.storeName)) {
                    const objectStore = db.createObjectStore(this.storeName, { 
                        keyPath: 'id', 
                        autoIncrement: true 
                    });
                    
                    // Create indexes
                    objectStore.createIndex('name', 'name', { unique: false });
                    objectStore.createIndex('email', 'email', { unique: true });
                    objectStore.createIndex('department', 'department', { unique: false });
                }
                
                // Create syncActions object store if it doesn't exist
                if (!db.objectStoreNames.contains(this.syncActionsStoreName)) {
                    const syncStore = db.createObjectStore(this.syncActionsStoreName, { 
                        keyPath: 'id', 
                        autoIncrement: true 
                    });
                    
                    syncStore.createIndex('isSynced', 'isSynced', { unique: false });
                    syncStore.createIndex('timestamp', 'timestamp', { unique: false });
                }
            };
        });
    },

    // Ensure database is initialized
    async ensureInit() {
        if (!this.db) {
            await this.init();
        }
        return this.db;
    },

    // Add a new employee
    async addEmployee(employeeJson) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            try {
                const transaction = this.db.transaction([this.storeName], 'readwrite');
                const store = transaction.objectStore(this.storeName);
                
                const employee = typeof employeeJson === 'string' ? JSON.parse(employeeJson) : employeeJson;
                
                // Convert dates to ISO strings for storage
                // NEVER include id field for new employees - let autoIncrement handle it
                const employeeToStore = {
                    name: employee.name || '',
                    email: employee.email || '',
                    department: employee.department || '',
                    position: employee.position || '',
                    hireDate: employee.hireDate ? new Date(employee.hireDate).toISOString() : new Date().toISOString()
                };

                // Do NOT include id - this allows autoIncrement to generate a new id
                // If employee has id > 0, that means it's an update, not an add

                const request = store.add(employeeToStore);

                request.onsuccess = () => {
                    // The result is the auto-generated id
                    const result = { id: request.result, ...employeeToStore };
                    resolve(JSON.stringify(result));
                };

                request.onerror = (event) => {
                    const error = event.target.error;
                    let errorMessage = 'Failed to add employee';
                    
                    if (error) {
                        if (error.name === 'ConstraintError') {
                            errorMessage = 'Employee with this email already exists or duplicate ID';
                        } else if (error.name === 'DataError') {
                            errorMessage = 'Invalid employee data';
                        } else {
                            errorMessage = `Failed to add employee: ${error.message || error.name}`;
                        }
                    }
                    
                    console.error('IndexedDB add error:', error);
                    console.error('Employee data:', employeeToStore);
                    reject(new Error(errorMessage));
                };
                
                transaction.onerror = (event) => {
                    console.error('Transaction error:', event);
                    reject(new Error('Transaction failed: ' + (event.target.error?.message || 'Unknown error')));
                };
            } catch (ex) {
                console.error('Exception in addEmployee:', ex);
                reject(new Error('Failed to add employee: ' + ex.message));
            }
        });
    },

    // Get all employees
    async getAllEmployees() {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);
            const request = store.getAll();

            request.onsuccess = () => {
                const employees = request.result.map(emp => ({
                    ...emp,
                    hireDate: emp.hireDate ? new Date(emp.hireDate).toISOString() : null
                }));
                resolve(JSON.stringify(employees));
            };

            request.onerror = () => {
                reject(new Error('Failed to get employees'));
            };
        });
    },

    // Get employee by ID
    async getEmployeeById(id) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);
            const request = store.get(id);

            request.onsuccess = () => {
                if (request.result) {
                    const employee = {
                        ...request.result,
                        hireDate: request.result.hireDate ? new Date(request.result.hireDate).toISOString() : null
                    };
                    resolve(JSON.stringify(employee));
                } else {
                    resolve('null');
                }
            };

            request.onerror = () => {
                reject(new Error('Failed to get employee'));
            };
        });
    },

    // Update employee (or add if doesn't exist)
    async updateEmployee(employeeJson) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            try {
                const transaction = this.db.transaction([this.storeName], 'readwrite');
                const store = transaction.objectStore(this.storeName);
                
                const employee = typeof employeeJson === 'string' ? JSON.parse(employeeJson) : employeeJson;
                
                // Ensure employee has all required fields
                if (!employee.id && employee.id !== 0) {
                    reject(new Error('Employee must have an ID to update'));
                    return;
                }
                
                // Convert dates to ISO strings for storage
                const employeeToStore = {
                    id: employee.id,
                    name: employee.name || '',
                    email: employee.email || '',
                    department: employee.department || '',
                    position: employee.position || '',
                    hireDate: employee.hireDate ? new Date(employee.hireDate).toISOString() : new Date().toISOString()
                };

                // Use put() which will add if doesn't exist, or update if exists
                const request = store.put(employeeToStore);

                request.onsuccess = () => {
                    resolve(JSON.stringify(employeeToStore));
                };

                request.onerror = (event) => {
                    const error = event.target.error;
                    let errorMessage = 'Failed to update employee';
                    
                    if (error) {
                        if (error.name === 'ConstraintError') {
                            errorMessage = 'Employee with this email already exists';
                        } else {
                            errorMessage = `Failed to update employee: ${error.message || error.name}`;
                        }
                    }
                    
                    console.error('IndexedDB update error:', error);
                    console.error('Employee data:', employeeToStore);
                    reject(new Error(errorMessage));
                };
                
                transaction.onerror = (event) => {
                    console.error('Transaction error:', event);
                    reject(new Error('Transaction failed: ' + (event.target.error?.message || 'Unknown error')));
                };
            } catch (ex) {
                console.error('Exception in updateEmployee:', ex);
                reject(new Error('Failed to update employee: ' + ex.message));
            }
        });
    },

    // Delete employee
    async deleteEmployee(id) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const request = store.delete(id);

            request.onsuccess = () => {
                resolve(true);
            };

            request.onerror = () => {
                reject(new Error('Failed to delete employee'));
            };
        });
    },

    // Clear all employees (optional utility)
    async clearAllEmployees() {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const request = store.clear();

            request.onsuccess = () => {
                resolve(true);
            };

            request.onerror = () => {
                reject(new Error('Failed to clear employees'));
            };
        });
    },

    // Sync Actions Management
    async addSyncAction(actionJson) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.syncActionsStoreName], 'readwrite');
            const store = transaction.objectStore(this.syncActionsStoreName);
            
            const action = typeof actionJson === 'string' ? JSON.parse(actionJson) : actionJson;
            const actionToStore = {
                ...action,
                timestamp: action.timestamp ? new Date(action.timestamp).toISOString() : new Date().toISOString(),
                isSynced: false
            };

            const request = store.add(actionToStore);

            request.onsuccess = () => {
                resolve(request.result);
            };

            request.onerror = () => {
                reject(new Error('Failed to add sync action'));
            };
        });
    },

    async getAllPendingSyncActions() {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.syncActionsStoreName], 'readonly');
            const store = transaction.objectStore(this.syncActionsStoreName);
            const index = store.index('isSynced');
            const request = index.getAll(false); // Get all non-synced actions

            request.onsuccess = () => {
                const actions = request.result.map(action => ({
                    ...action,
                    timestamp: action.timestamp ? new Date(action.timestamp).toISOString() : null
                }));
                resolve(JSON.stringify(actions));
            };

            request.onerror = () => {
                reject(new Error('Failed to get pending sync actions'));
            };
        });
    },

    async markSyncActionAsSynced(actionId) {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.syncActionsStoreName], 'readwrite');
            const store = transaction.objectStore(this.syncActionsStoreName);
            const getRequest = store.get(actionId);

            getRequest.onsuccess = () => {
                if (getRequest.result) {
                    const action = getRequest.result;
                    action.isSynced = true;
                    const putRequest = store.put(action);
                    
                    putRequest.onsuccess = () => {
                        resolve(true);
                    };
                    
                    putRequest.onerror = () => {
                        reject(new Error('Failed to mark sync action as synced'));
                    };
                } else {
                    resolve(false);
                }
            };

            getRequest.onerror = () => {
                reject(new Error('Failed to get sync action'));
            };
        });
    },

    async removeSyncedActions() {
        await this.ensureInit();
        
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.syncActionsStoreName], 'readwrite');
            const store = transaction.objectStore(this.syncActionsStoreName);
            const index = store.index('isSynced');
            const request = index.openCursor(IDBKeyRange.only(true));

            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    cursor.delete();
                    cursor.continue();
                } else {
                    resolve(true);
                }
            };

            request.onerror = () => {
                reject(new Error('Failed to remove synced actions'));
            };
        });
    }
};

