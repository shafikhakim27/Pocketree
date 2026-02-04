package com.pocketree.app

import android.util.Log
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import io.reactivex.rxjava3.core.Single

// done by George (Ching Tard), edited by shirley
object SignalRManager {
    private var hubConnection: HubConnection? = null

    // Initialize the connection once with the JWT token
    fun init(token: String, viewModel: UserViewModel) {
        if (hubConnection == null) {
            hubConnection = HubConnectionBuilder.create("http://10.0.2.2:5042/notificationHub")
                .withAccessTokenProvider(Single.just(token))
                .build()

            // Setup the 'ReceiveMessage' listener to send a broadcast
            hubConnection?.on("ReceiveMessage", { message ->
                viewModel.postAdminMessage(message)
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