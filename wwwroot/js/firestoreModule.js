// wwwroot/js/firestoreModule.js

// Firestore module for Blazor WASM interop
window.firestoreModule = (function () {
    let db = null;
    let isInitialized = false;

    // Initialize Firestore
    async function initializeFirestore() {
        try {
            if (!firebase || !firebase.firestore) {
                console.error("Firebase or Firestore is not loaded");
                return false;
            }

            db = firebase.firestore();
            isInitialized = true;
            console.log("Firestore initialized successfully");
            return true;
        } catch (error) {
            console.error("Error initializing Firestore:", error);
            return false;
        }
    }

    // Get a document by ID
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
                console.log("No such document!");
                return null;
            }
        } catch (error) {
            console.error("Error getting document:", error);
            return null;
        }
    }

    // Add a new document
    async function addDocument(collection, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            const data = JSON.parse(jsonData);
            const docRef = await db.collection(collection).add(data);
            return docRef.id;
        } catch (error) {
            console.error("Error adding document:", error);
            return null;
        }
    }

    // Update a document
    async function updateDocument(collection, id, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            const data = JSON.parse(jsonData);
            await db.collection(collection).doc(id).update(data);
            return true;
        } catch (error) {
            console.error("Error updating document:", error);
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
            console.error("Error deleting document:", error);
            return false;
        }
    }

    // Get all documents in a collection
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
            console.error("Error getting collection:", error);
            return JSON.stringify([]);
        }
    }

    // Query a collection by field
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
            console.error("Error querying collection:", error);
            return JSON.stringify([]);
        }
    }

    // Add multiple documents in a batch
    async function addBatch(collection, jsonItems) {
        try {
            if (!isInitialized) await initializeFirestore();

            const items = JSON.parse(jsonItems);
            const batch = db.batch();

            items.forEach((item) => {
                const docRef = db.collection(collection).doc();
                batch.set(docRef, item);
            });

            await batch.commit();
            return true;
        } catch (error) {
            console.error("Error adding batch:", error);
            return false;
        }
    }

    // Sync local data with Firestore (merge strategy)
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

            // Update or add items based on a unique field (assuming matriculationNumber for users)
            for (const item of localItems) {
                // If the item has an ID, update it directly
                if (item.id && existingDocs[item.id]) {
                    batch.update(db.collection(collection).doc(item.id), item);
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
                        batch.update(docToUpdate.ref, item);
                    } else {
                        batch.set(db.collection(collection).doc(), item);
                    }
                }
            }

            await batch.commit();
            return true;
        } catch (error) {
            console.error("Error syncing collection:", error);
            return false;
        }
    }

    // Check if Firestore is connected
    async function isConnected() {
        try {
            // Get Firestore connection state
            if (!isInitialized) return false;

            const connectedRef = firebase.database().ref(".info/connected");
            return new Promise((resolve) => {
                connectedRef.on("value", (snap) => {
                    resolve(snap.val() === true);
                });
            });
        } catch (error) {
            console.error("Error checking connection:", error);
            return false;
        }
    }

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
        isConnected
    };
})();