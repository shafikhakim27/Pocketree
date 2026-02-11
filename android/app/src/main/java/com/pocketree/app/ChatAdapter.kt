package com.pocketree.app

import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class ChatAdapter (private val messages: List<ChatMessage>):
    RecyclerView.Adapter<RecyclerView.ViewHolder>() {

    private val VIEW_TYPE_USER = 1
    private val VIEW_TYPE_AI = 2

    override fun getItemViewType(position:Int): Int{
        val message = messages[position]
        val viewType = if (message.isUser) VIEW_TYPE_USER else VIEW_TYPE_AI
        return viewType
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType:Int): RecyclerView.ViewHolder {
        val inflater = LayoutInflater.from(parent.context)
        return if (viewType == VIEW_TYPE_USER) {
            val view = inflater.inflate(R.layout.item_chat_user, parent, false)
            UserViewHolder(view)
        } else {
            val view = inflater.inflate(R.layout.item_chat_ai, parent, false)
            AIViewHolder(view)
        }
    }

    override fun onBindViewHolder(holder: RecyclerView.ViewHolder, position: Int) {
        val message = messages[position]

        if (holder is UserViewHolder) {
            holder.messageText.text = message.text
        } else if (holder is AIViewHolder) {
            holder.messageText.text = message.text
        }
    }

    override fun getItemCount() = messages.size

    class UserViewHolder(view: View): RecyclerView.ViewHolder(view) {
        val messageText: TextView = view.findViewById(R.id.text_message_user)
    }

    class AIViewHolder(view:View): RecyclerView.ViewHolder(view) {
        val messageText: TextView = view.findViewById(R.id.text_message_ai)
    }
}
