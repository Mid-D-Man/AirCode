// wwwroot/js/firestoreModule.js

// Firestore module for Blazor WASM interop
window.firestoreModule = (function () {
    let db = null;
    let isInitialized = false;
    let isOffline = false;

    // Initialize Firestore with better error handling
    async function initializeFirestore() {
        try {
            if (!firebase || !firebase.firestore) {
                console.error("Firebase or Firestore is not loaded");
                return false;
            }

            db = firebase.firestore();

            // Enable offline persistence
            db.enablePersistence({ synchronizeTabs: true })
                .catch((err) => {
                    if (err.code === 'failed-precondition') {
                        // Multiple tabs open, persistence can only be enabled in one tab
                        console.warn("Persistence failed: Multiple tabs open");
                    } else if (err.code === 'unimplemented') {
                        // Current browser doesn't support persistence
                        console.warn("Persistence not supported in this browser");
                    }
                });

            // Monitor connection state
            firebase.database().ref(".info/connected").on("value", (snapshot) => {
                isOffline = !snapshot.val();
                console.log("Connection state:", isOffline ? "Offline" : "Online");
            });

            isInitialized = true;
            console.log("Firestore initialized successfully");
            return true;
        } catch (error) {
            console.error("Error initializing Firestore:", error);
            return false;
        }
    }

    // Get a document by ID with better error handling
    async function getDocument(collection, id) {
        try {
            if (!isInitialized) await initializeFirestore();

            const docRef = db.collection(collection).doc(id);
            const doc = await docRef.get();

            if (doc.exists) {
                const data = doc.data();
                data.id = doc.id; // Add ID to the data
                return JSON.stringify(data);
            } else {
                console.log(`Document not found: ${collection}/${id}`);
                return null;
            }
        } catch (error) {
            console.error(`Error getting document ${collection}/${id}:`, error);
            return null;
        }
    }

    // Add a new document with custom ID option
    async function addDocument(collection, jsonData, customId = null) {
        try {
            if (!isInitialized) await initializeFirestore();

            const data = JSON.parse(jsonData);
            let docRef;

            if (customId) {
                // Use custom ID if provided
                docRef = db.collection(collection).doc(customId);
                await docRef.set(data);
                return customId;
            } else {
                // Let Firestore generate an ID
                docRef = await db.collection(collection).add(data);
                return docRef.id;
            }
        } catch (error) {
            console.error("Error adding document:", error);

            // Store locally if offline
            if (isOffline) {
                const offlineData = {
                    collection,
                    data: jsonData,
                    operation: 'add',
                    timestamp: new Date().getTime()
                };
                storeOfflineOperation(offlineData);
            }

            return null;
        }
    }

    // Update a document with better error handling
    async function updateDocument(collection, id, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            const data = JSON.parse(jsonData);
            await db.collection(collection).doc(id).update(data);
            return true;
        } catch (error) {
            console.error(`Error updating document ${collection}/${id}:`, error);

            // Store locally if offline
            if (isOffline) {
                const offlineData = {
                    collection,
                    id,
                    data: jsonData,
                    operation: 'update',
                    timestamp: new Date().getTime()
                };
                storeOfflineOperation(offlineData);
            }

            return false;
        }
    }

    // Delete a document with better error handling
    async function deleteDocument(collection, id) {
        try {
            if (!isInitialized) await initializeFirestore();

            await db.collection(collection).doc(id).delete();
            return true;
        } catch (error) {
            console.error(`Error deleting document ${collection}/${id}:`, error);

            // Store locally if offline
            if (isOffline) {
                const offlineData = {
                    collection,
                    id,
                    operation: 'delete',
                    timestamp: new Date().getTime()
                };
                storeOfflineOperation(offlineData);
            }

            return false;
        }
    }

    // Get all documents in a collection with better error handling
    async function getCollection(collection) {
        try {
            if (!isInitialized) await initializeFirestore();

            const querySnapshot = await db.collection(collection).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                item.id = doc.id; // Add ID to each document
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error getting collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    // Query a collection by field with better error handling
    async function queryCollection(collection, field, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            const value = JSON.parse(jsonValue);
            const querySnapshot = await db.collection(collection).where(field, "==", value).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                item.id = doc.id;
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error querying collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    // Add multiple documents in a batch with better error handling
    async function addBatch(collection, jsonItems) {
        try {
            if (!isInitialized) await initializeFirestore();

            const items = JSON.parse(jsonItems);
            const batch = db.batch();

            items.forEach((item) => {
                // Allow using custom IDs if available in the item
                const docId = item.id || db.collection(collection).doc().id;
                const docRef = db.collection(collection).doc(docId);

                // Remove the id field before setting the document
                if (item.id) {
                    const { id, ...dataWithoutId } = item;
                    batch.set(docRef, dataWithoutId);
                } else {
                    batch.set(docRef, item);
                }
            });

            await batch.commit();
            return true;
        } catch (error) {
            console.error(`Error adding batch to ${collection}:`, error);

            // Store locally if offline
            if (isOffline) {
                const offlineData = {
                    collection,
                    data: jsonItems,
                    operation: 'batch',
                    timestamp: new Date().getTime()
                };
                storeOfflineOperation(offlineData);
            }

            return false;
        }
    }

    // Store offline operations for later sync
    function storeOfflineOperation(operation) {
        try {
            const storageKey = 'firestore_offline_operations';
            const existingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            existingOps.push(operation);
            localStorage.setItem(storageKey, JSON.stringify(existingOps));
            console.log('Operation stored for offline use:', operation);
        } catch (error) {
            console.error('Error storing offline operation:', error);
        }
    }

    // Process any pending offline operations when back online
    async function processPendingOperations() {
        if (!navigator.onLine || !isInitialized) return;

        const storageKey = 'firestore_offline_operations';
        try {
            const pendingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            if (pendingOps.length === 0) return;

            console.log(`Processing ${pendingOps.length} pending operations`);

            // Sort by timestamp (oldest first)
            pendingOps.sort((a, b) => a.timestamp - b.timestamp);

            const successfulOps = [];

            for (const op of pendingOps) {
                try {
                    let success = false;

                    switch (op.operation) {
                        case 'add':
                            const addResult = await addDocument(op.collection, op.data, op.id);
                            success = !!addResult;
                            break;
                        case 'update':
                            success = await updateDocument(op.collection, op.id, op.data);
                            break;
                        case 'delete':
                            success = await deleteDocument(op.collection, op.id);
                            break;
                        case 'batch':
                            success = await addBatch(op.collection, op.data);
                            break;
                    }

                    if (success) {
                        successfulOps.push(op);
                    }
                } catch (error) {
                    console.error('Error processing pending operation:', error, op);
                }
            }

            // Remove successful operations
            const remainingOps = pendingOps.filter(op =>
                !successfulOps.some(sop =>
                    sop.timestamp === op.timestamp &&
                    sop.operation === op.operation
                )
            );

            localStorage.setItem(storageKey, JSON.stringify(remainingOps));
            console.log(`Processed ${successfulOps.length} operations, ${remainingOps.length} remaining`);

        } catch (error) {
            console.error('Error processing pending operations:', error);
        }
    }

    // Sync local data with Firestore
    async function syncCollectionWithLocal(collection, jsonItems) {
        try {
            if (!isInitialized) await initializeFirestore();

            const localItems = JSON.parse(jsonItems);
            const batch = db.batch();

            // Get existing items to compare
            const querySnapshot = await db.collection(collection).get();
            const existingDocs = {};

            querySnapshot.forEach((doc) => {
                existingDocs[doc.id] = doc;
            });

            // Update or add items based on a unique field
            for (const item of localItems) {
                // If the item has an ID, update it directly
                if (item.id && existingDocs[item.id]) {
                    // Remove the id field before updating
                    const { id, ...dataWithoutId } = item;
                    batch.update(db.collection(collection).doc(item.id), dataWithoutId);
                } else {
                    // Otherwise query to find if it exists by a unique field
                    let docToUpdate = null;

                    // For users, check by matriculationNumber
                    if (item.matriculationNumber) {
                        const matchQuery = await db.collection(collection)
                            .where("matriculationNumber", "==", item.matriculationNumber)
                            .limit(1)
                            .get();

                        if (!matchQuery.empty) {
                            docToUpdate = matchQuery.docs[0];
                        }
                    }

                    if (docToUpdate) {
                        // Remove the id field if it exists
                        const { id, ...dataWithoutId } = item;
                        batch.update(docToUpdate.ref, dataWithoutId);
                    } else {
                        // Create new document with custom ID if provided
                        const docRef = item.id ?
                            db.collection(collection).doc(item.id) :
                            db.collection(collection).doc();

                        // Remove the id field before setting
                        if (item.id) {
                            const { id, ...dataWithoutId } = item;
                            batch.set(docRef, dataWithoutId);
                        } else {
                            batch.set(docRef, item);
                        }
                    }
                }
            }

            await batch.commit();
            return true;
        } catch (error) {
            console.error(`Error syncing collection ${collection}:`, error);
            return false;
        }
    }

    // Check if Firestore is connected
    async function isConnected() {
        try {
            // Get Firestore connection state
            if (!isInitialized) {
                const initResult = await initializeFirestore();
                if (!initResult) return false;
            }

            return new Promise((resolve) => {
                const connectedRef = firebase.database().ref(".info/connected");
                connectedRef.on("value", (snap) => {
                    const connected = snap.val() === true;
                    isOffline = !connected;
                    resolve(connected);
                });
            });
        } catch (error) {
            console.error("Error checking connection:", error);
            return false;
        }
    }

    // Process pending operations when online
    window.addEventListener('online', () => {
        console.log('Back online, processing pending operations');
        processPendingOperations();
    });

    return {
        initializeFirestore,
        getDocument,
        addDocument,
        updateDocument,
        deleteDocument,
        getCollection,
        queryCollection,
        addBatch,
        syncCollectionWithLocal,
        isConnected,
        processPendingOperations
    };
})();