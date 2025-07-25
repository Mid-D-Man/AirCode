// wwwroot/js/firestoreNotificationModule.js

window.firestoreNotificationModule = (function () {
    let db = null;
    let activeListeners = new Map(); // Track active listeners by ID
    let dotNetReference = null;

    // Initialize with main firestore module dependency
    async function initialize(dotNetRef) {
        try {
            dotNetReference = dotNetRef;

            // Wait for main firestore module to be ready
            if (!window.firestoreModule) {
                throw new Error("Main firestoreModule not loaded");
            }

            // Initialize main module if needed
            await window.firestoreModule.initializeFirestore();
            db = firebase.firestore();

            console.log("Notification module initialized");
            return true;
        } catch (error) {
            console.error("Error initializing notification module:", error);
            return false;
        }
    }

    // Generic document listener
    function subscribeToDocument(listenerId, collection, documentId, callbackMethod = "OnDocumentChanged") {
        try {
            if (!db) throw new Error("Module not initialized");

            // Remove existing listener with same ID
            if (activeListeners.has(listenerId)) {
                unsubscribe(listenerId);
            }

            const docRef = db.collection(collection).doc(documentId);
            const unsubscribeFunc = docRef.onSnapshot({
                includeMetadataChanges: false
            }, (doc) => {
                const eventData = {
                    listenerId: listenerId,
                    collection: collection,
                    documentId: documentId,
                    exists: doc.exists,
                    data: doc.exists ? doc.data() : null,
                    timestamp: new Date().toISOString()
                };

                // Add document ID to data if exists
                if (eventData.data) {
                    eventData.data.id = doc.id;
                }

                // Notify C# service
                if (dotNetReference) {
                    dotNetReference.invokeMethodAsync(callbackMethod, JSON.stringify(eventData))
                        .catch(err => console.error("Error calling C# callback:", err));
                }

                console.log(`Document change detected: ${collection}/${documentId}`);
            }, (error) => {
                console.error(`Listener error for ${listenerId}:`, error);

                // Notify C# of error
                if (dotNetReference) {
                    const errorData = {
                        listenerId: listenerId,
                        error: error.message,
                        timestamp: new Date().toISOString()
                    };
                    dotNetReference.invokeMethodAsync("OnListenerError", JSON.stringify(errorData))
                        .catch(err => console.error("Error calling C# error callback:", err));
                }
            });

            // Store listener reference
            activeListeners.set(listenerId, {
                unsubscribe: unsubscribeFunc,
                type: 'document',
                collection: collection,
                documentId: documentId,
                createdAt: new Date()
            });

            console.log(`Document listener created: ${listenerId}`);
            return true;
        } catch (error) {
            console.error(`Error creating document listener ${listenerId}:`, error);
            return false;
        }
    }

    // Generic collection listener
    function subscribeToCollection(listenerId, collection, whereField = null, whereValue = null, callbackMethod = "OnCollectionChanged") {
        try {
            if (!db) throw new Error("Module not initialized");

            // Remove existing listener with same ID
            if (activeListeners.has(listenerId)) {
                unsubscribe(listenerId);
            }

            let query = db.collection(collection);

            // Apply where clause if provided
            if (whereField && whereValue !== null) {
                const parsedValue = typeof whereValue === 'string' ? JSON.parse(whereValue) : whereValue;
                query = query.where(whereField, "==", parsedValue);
            }

            const unsubscribeFunc = query.onSnapshot({
                includeMetadataChanges: false
            }, (snapshot) => {
                const changes = [];

                snapshot.docChanges().forEach((change) => {
                    const docData = change.doc.data();
                    docData.id = change.doc.id;

                    changes.push({
                        type: change.type, // 'added', 'modified', 'removed'
                        document: docData,
                        oldIndex: change.oldIndex,
                        newIndex: change.newIndex
                    });
                });

                const eventData = {
                    listenerId: listenerId,
                    collection: collection,
                    changes: changes,
                    size: snapshot.size,
                    timestamp: new Date().toISOString()
                };

                // Notify C# service
                if (dotNetReference) {
                    dotNetReference.invokeMethodAsync(callbackMethod, JSON.stringify(eventData))
                        .catch(err => console.error("Error calling C# callback:", err));
                }

                console.log(`Collection changes detected: ${collection}, ${changes.length} changes`);
            }, (error) => {
                console.error(`Collection listener error for ${listenerId}:`, error);

                // Notify C# of error
                if (dotNetReference) {
                    const errorData = {
                        listenerId: listenerId,
                        error: error.message,
                        timestamp: new Date().toISOString()
                    };
                    dotNetReference.invokeMethodAsync("OnListenerError", JSON.stringify(errorData))
                        .catch(err => console.error("Error calling C# error callback:", err));
                }
            });

            // Store listener reference  
            activeListeners.set(listenerId, {
                unsubscribe: unsubscribeFunc,
                type: 'collection',
                collection: collection,
                whereField: whereField,
                whereValue: whereValue,
                createdAt: new Date()
            });

            console.log(`Collection listener created: ${listenerId}`);
            return true;
        } catch (error) {
            console.error(`Error creating collection listener ${listenerId}:`, error);
            return false;
        }
    }

    // Attendance session specific listener (your main focus)
    function subscribeToAttendanceSession(sessionId, callbackMethod = "OnAttendanceSessionChanged") {
        const listenerId = `attendance_session_${sessionId}`;
        return subscribeToDocument(listenerId, "AttendanceSessions", sessionId, callbackMethod);
    }

    // Listen for active attendance sessions
    function subscribeToActiveAttendanceSessions(callbackMethod = "OnActiveSessionsChanged") {
        const listenerId = "active_attendance_sessions";
        return subscribeToCollection(listenerId, "AttendanceSessions", "isActive", "true", callbackMethod);
    }

    // Unsubscribe from a specific listener
    function unsubscribe(listenerId) {
        try {
            if (activeListeners.has(listenerId)) {
                const listener = activeListeners.get(listenerId);
                listener.unsubscribe();
                activeListeners.delete(listenerId);
                console.log(`Listener unsubscribed: ${listenerId}`);
                return true;
            }
            return false;
        } catch (error) {
            console.error(`Error unsubscribing ${listenerId}:`, error);
            return false;
        }
    }

    // Unsubscribe from all listeners
    function unsubscribeAll() {
        try {
            let count = 0;
            for (const [listenerId, listener] of activeListeners) {
                listener.unsubscribe();
                count++;
            }
            activeListeners.clear();
            console.log(`All listeners unsubscribed: ${count} total`);
            return count;
        } catch (error) {
            console.error("Error unsubscribing all listeners:", error);
            return 0;
        }
    }

    // Get active listeners info
    function getActiveListeners() {
        const listeners = [];
        for (const [listenerId, listener] of activeListeners) {
            listeners.push({
                id: listenerId,
                type: listener.type,
                collection: listener.collection,
                documentId: listener.documentId || null,
                whereField: listener.whereField || null,
                whereValue: listener.whereValue || null,
                createdAt: listener.createdAt.toISOString()
            });
        }
        return JSON.stringify(listeners);
    }

    // Cleanup on page unload
    window.addEventListener('beforeunload', () => {
        unsubscribeAll();
    });

    return {
        // Core functions
        initialize,
        subscribeToDocument,
        subscribeToCollection,
        unsubscribe,
        unsubscribeAll,
        getActiveListeners,

        // Attendance-specific functions
        subscribeToAttendanceSession,
        subscribeToActiveAttendanceSessions
    };
})();