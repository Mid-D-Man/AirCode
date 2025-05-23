// wwwroot/js/firestoreModule.js

// Enhanced Firestore module for Blazor WASM interop with document/field/subcollection support
window.firestoreModule = (function () {
    let db = null;
    let isInitialized = false;
    let isOffline = false;
    let manuallyDisconnected = false;

    // Initialize Firestore with better error handling
    async function initializeFirestore() {
        try {
            if (!firebase || !firebase.firestore) {
                console.error("Firebase or Firestore is not loaded");
                return false;
            }

            // Configure settings BEFORE getting instance
            if (!isInitialized) {
                db = firebase.firestore();

                // Only set settings on first initialization
                db.settings({
                    ignoreUndefinedProperties: true,
                    timestampsInSnapshots: true
                });

                // Enable persistence only once
                try {
                    await db.enablePersistence({ synchronizeTabs: true });
                } catch (err) {
                    if (err.code === 'failed-precondition') {
                        console.warn("Persistence failed: Multiple tabs open");
                    } else if (err.code === 'unimplemented') {
                        console.warn("Persistence not supported in this browser");
                    }
                }

                isInitialized = true;
            }

            // Connection monitoring (can be called multiple times)
            firebase.database().ref(".info/connected").on("value", (snapshot) => {
                if (!manuallyDisconnected) {
                    isOffline = !snapshot.val();
                    console.log("Connection state:", isOffline ? "Offline" : "Online");
                }
            });

            console.log("Firestore initialized successfully");
            return true;
        } catch (error) {
            console.error("Error initializing Firestore:", error);
            return false;
        }
    }

    // Set connection state manually
    async function setConnectionState(connect) {
        try {
            if (!isInitialized) await initializeFirestore();

            manuallyDisconnected = !connect;

            if (connect) {
                await firebase.firestore().enableNetwork();
                isOffline = !navigator.onLine;
                console.log("Firebase connection manually enabled");
            } else {
                await firebase.firestore().disableNetwork();
                isOffline = true;
                console.log("Firebase connection manually disabled");
            }

            return true;
        } catch (error) {
            console.error("Error setting connection state:", error);
            return false;
        }
    }

    // ==================== DOCUMENT OPERATIONS ====================

    // Get a document by ID
    async function getDocument(collection, id) {
        try {
            if (!isInitialized) await initializeFirestore();

            const docRef = db.collection(collection).doc(id);
            const doc = await docRef.get();

            if (doc.exists) {
                const data = doc.data();
                if (data && typeof data === 'object') {
                    data.id = doc.id;
                }
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

    // Add a new document
    async function addDocument(collection, jsonData, customId = null) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = JSON.parse(JSON.stringify(data)); // Remove undefined values

            let docRef;
            if (customId) {
                docRef = db.collection(collection).doc(customId);
                await docRef.set(data);
                return customId;
            } else {
                docRef = await db.collection(collection).add(data);
                return docRef.id;
            }
        } catch (error) {
            console.error("Error adding document:", error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonData, operation: 'add', timestamp: Date.now() });
            return null;
        }
    }

    // Update entire document
    async function updateDocument(collection, id, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = removeUndefinedConservative(data);

            await db.collection(collection).doc(id).update(data);
            console.log(`Document ${collection}/${id} updated successfully`);
            return true;
        } catch (error) {
            console.error(`Error updating document ${collection}/${id}:`, error);
            return false;
        }
    }

    // Delete a document
    async function deleteDocument(collection, id) {
        try {
            if (!isInitialized) await initializeFirestore();

            await db.collection(collection).doc(id).delete();
            return true;
        } catch (error) {
            console.error(`Error deleting document ${collection}/${id}:`, error);
            if (isOffline) storeOfflineOperation({ collection, id, operation: 'delete', timestamp: Date.now() });
            return false;
        }
    }

    // ==================== FIELD OPERATIONS ====================

    // Add/Update a specific field in a document
    async function addOrUpdateField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = value;

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Field ${fieldName} updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error updating field ${fieldName}:`, error);
            return false;
        }
    }

    // Add/Update multiple fields in a document
    async function updateFields(collection, docId, jsonFields) {
        try {
            if (!isInitialized) await initializeFirestore();

            let fields = JSON.parse(jsonFields);
            fields = removeUndefinedConservative(fields);

            await db.collection(collection).doc(docId).update(fields);
            console.log(`Multiple fields updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error updating fields in ${collection}/${docId}:`, error);
            return false;
        }
    }

    // Remove a specific field from a document
    async function removeField(collection, docId, fieldName) {
        try {
            if (!isInitialized) await initializeFirestore();

            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.delete();

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Field ${fieldName} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error removing field ${fieldName}:`, error);
            return false;
        }
    }

    // Remove multiple fields from a document
    async function removeFields(collection, docId, fieldNames) {
        try {
            if (!isInitialized) await initializeFirestore();

            const fieldsArray = JSON.parse(fieldNames);
            const updateData = {};

            fieldsArray.forEach(fieldName => {
                updateData[fieldName] = firebase.firestore.FieldValue.delete();
            });

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Fields ${fieldsArray.join(', ')} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error removing fields:`, error);
            return false;
        }
    }

    // Get specific field value from a document
    async function getField(collection, docId, fieldName) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(docId).get();

            if (doc.exists) {
                const data = doc.data();
                const fieldValue = data[fieldName];
                return fieldValue !== undefined ? JSON.stringify(fieldValue) : null;
            }
            return null;
        } catch (error) {
            console.error(`Error getting field ${fieldName}:`, error);
            return null;
        }
    }

    // ==================== SUBCOLLECTION OPERATIONS ====================

    // Add document to subcollection
    async function addToSubcollection(collection, docId, subcollection, jsonData, customId = null) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = JSON.parse(JSON.stringify(data));

            const subcollectionRef = db.collection(collection).doc(docId).collection(subcollection);

            let docRef;
            if (customId) {
                docRef = subcollectionRef.doc(customId);
                await docRef.set(data);
                return customId;
            } else {
                docRef = await subcollectionRef.add(data);
                return docRef.id;
            }
        } catch (error) {
            console.error(`Error adding to subcollection ${subcollection}:`, error);
            return null;
        }
    }

    // Get all documents from a subcollection
    async function getSubcollection(collection, docId, subcollection) {
        try {
            if (!isInitialized) await initializeFirestore();

            const subcollectionRef = db.collection(collection).doc(docId).collection(subcollection);
            const querySnapshot = await subcollectionRef.get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error getting subcollection ${subcollection}:`, error);
            return JSON.stringify([]);
        }
    }

    // Get specific document from subcollection
    async function getSubcollectionDocument(collection, docId, subcollection, subdocId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const subdocRef = db.collection(collection).doc(docId).collection(subcollection).doc(subdocId);
            const doc = await subdocRef.get();

            if (doc.exists) {
                const data = doc.data();
                if (data && typeof data === 'object') {
                    data.id = doc.id;
                }
                return JSON.stringify(data);
            }
            return null;
        } catch (error) {
            console.error(`Error getting subcollection document:`, error);
            return null;
        }
    }

    // Update document in subcollection
    async function updateSubcollectionDocument(collection, docId, subcollection, subdocId, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = removeUndefinedConservative(data);

            const subdocRef = db.collection(collection).doc(docId).collection(subcollection).doc(subdocId);
            await subdocRef.update(data);

            console.log(`Subcollection document updated: ${collection}/${docId}/${subcollection}/${subdocId}`);
            return true;
        } catch (error) {
            console.error(`Error updating subcollection document:`, error);
            return false;
        }
    }

    // Delete document from subcollection
    async function deleteSubcollectionDocument(collection, docId, subcollection, subdocId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const subdocRef = db.collection(collection).doc(docId).collection(subcollection).doc(subdocId);
            await subdocRef.delete();

            console.log(`Subcollection document deleted: ${collection}/${docId}/${subcollection}/${subdocId}`);
            return true;
        } catch (error) {
            console.error(`Error deleting subcollection document:`, error);
            return false;
        }
    }

    // Query subcollection
    async function querySubcollection(collection, docId, subcollection, field, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const subcollectionRef = db.collection(collection).doc(docId).collection(subcollection);
            const querySnapshot = await subcollectionRef.where(field, "==", value).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error querying subcollection:`, error);
            return JSON.stringify([]);
        }
    }

    // ==================== ARRAY FIELD OPERATIONS ====================

    // Add item to array field
    async function addToArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayUnion(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Item added to array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`Error adding to array field ${fieldName}:`, error);
            return false;
        }
    }

    // Remove item from array field
    async function removeFromArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayRemove(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Item removed from array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`Error removing from array field ${fieldName}:`, error);
            return false;
        }
    }

    // ==================== UTILITY FUNCTIONS ====================

    // Conservative undefined removal that preserves structure
    function removeUndefinedConservative(obj) {
        if (obj === null || typeof obj !== 'object') return obj;

        if (Array.isArray(obj)) {
            return obj.map(removeUndefinedConservative);
        }

        const cleaned = {};
        for (const [key, value] of Object.entries(obj)) {
            if (value !== undefined) {
                if (typeof value === 'object' && value !== null) {
                    const cleanedValue = removeUndefinedConservative(value);
                    if (Array.isArray(cleanedValue) || Object.keys(cleanedValue).length > 0) {
                        cleaned[key] = cleanedValue;
                    }
                } else {
                    cleaned[key] = value;
                }
            }
        }
        return cleaned;
    }

    // ==================== COLLECTION OPERATIONS ====================

    // Get all documents in a collection
    async function getCollection(collection) {
        try {
            if (!isInitialized) await initializeFirestore();

            const querySnapshot = await db.collection(collection).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error getting collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    // Query a collection by field
    async function queryCollection(collection, field, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const querySnapshot = await db.collection(collection).where(field, "==", value).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error querying collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    // Find and delete course across multiple collections
    async function findAndDeleteCourse(courseCode) {
        try {
            if (!isInitialized) await initializeFirestore();

            const courseLevelCollections = [
                'Courses_100Level',
                'Courses_200Level',
                'Courses_300Level',
                'Courses_400Level',
                'Courses_500Level'
            ];

            for (const collection of courseLevelCollections) {
                try {
                    const querySnapshot = await db.collection(collection)
                        .where("courseCode", "==", courseCode)
                        .get();

                    if (!querySnapshot.empty) {
                        const docToDelete = querySnapshot.docs[0];
                        await docToDelete.ref.delete();
                        console.log(`Successfully deleted course ${courseCode} from ${collection}`);
                        return true;
                    }
                } catch (error) {
                    console.error(`Error searching in ${collection}:`, error);
                }
            }

            console.log(`Course ${courseCode} not found in any collection`);
            return false;
        } catch (error) {
            console.error("Error in findAndDeleteCourse:", error);
            return false;
        }
    }

    // Add multiple documents in a batch
    async function addBatch(collection, jsonItems) {
        try {
            if (!isInitialized) await initializeFirestore();

            let items = JSON.parse(jsonItems);
            items = JSON.parse(JSON.stringify(items));

            const batch = db.batch();

            items.forEach((item) => {
                const docId = item.id || db.collection(collection).doc().id;
                const docRef = db.collection(collection).doc(docId);
                const itemCopy = {...item};

                if ('id' in itemCopy) {
                    delete itemCopy.id;
                }

                batch.set(docRef, itemCopy);
            });

            await batch.commit();
            return true;
        } catch (error) {
            console.error(`Error adding batch to ${collection}:`, error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonItems, operation: 'batch', timestamp: Date.now() });
            return false;
        }
    }

    // ==================== OFFLINE SUPPORT ====================

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

    async function processPendingOperations() {
        if (!navigator.onLine || !isInitialized || manuallyDisconnected) return;

        const storageKey = 'firestore_offline_operations';
        try {
            const pendingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            if (pendingOps.length === 0) return;

            console.log(`Processing ${pendingOps.length} pending operations`);
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

    // Check if Firestore is connected
    async function isConnected() {
        try {
            if (manuallyDisconnected) return false;

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

    function getManualConnectionState() {
        return !manuallyDisconnected;
    }

    // Process pending operations when online
    window.addEventListener('online', () => {
        if (!manuallyDisconnected) {
            console.log('Back online, processing pending operations');
            processPendingOperations();
        }
    });

    return {
        // Initialization
        initializeFirestore,
        setConnectionState,
        getManualConnectionState,
        isConnected,
        processPendingOperations,

        // Document operations
        getDocument,
        addDocument,
        updateDocument,
        deleteDocument,

        // Field operations
        addOrUpdateField,
        updateFields,
        removeField,
        removeFields,
        getField,

        // Subcollection operations
        addToSubcollection,
        getSubcollection,
        getSubcollectionDocument,
        updateSubcollectionDocument,
        deleteSubcollectionDocument,
        querySubcollection,

        // Array field operations
        addToArrayField,
        removeFromArrayField,

        // Collection operations
        getCollection,
        queryCollection,
        addBatch,
        findAndDeleteCourse
    };
})();