package com.pocketree.app

import android.content.Context
import android.util.Log
import android.widget.Toast
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import io.reactivex.rxjava3.core.Single

// done by George (Ching Tard)
object SignalRManager {
    private var hubConnection: HubConnection? = null

    // Initialize the connection once with the JWT token
    fun init(token: String) {
        if (hubConnection == null) {
            val context = MyApplication.getContext()

            hubConnection = HubConnectionBuilder.create("http://10.0.2.2:5042/notificationHub")
                .withAccessTokenProvider(Single.just(token))
                .build()

            // Setup the 'ReceiveMessage' listener globally
            hubConnection?.on("ReceiveMessage", { message ->
                // This triggers regardless of which Activity is currently visible
                android.os.Handler(android.os.Looper.getMainLooper()).post {
                    Toast.makeText(context, "Admin: $message", Toast.LENGTH_LONG).show()
                }
            }, String::class.java)

            startConnection()
        }
    }

    private fun startConnection() {
        hubConnection?.start()?.subscribe({
            Log.d("SignalR", "Successfully connected to Hub")
        }, { error ->
            Log.e("SignalR", "Error connecting to SignalR: ${error.message}")
        })
    }

    fun stopConnection() {
        hubConnection?.stop()
        hubConnection = null
    }
}