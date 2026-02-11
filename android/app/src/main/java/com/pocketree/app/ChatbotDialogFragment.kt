package com.pocketree.app

import android.app.Dialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.android.material.bottomsheet.BottomSheetBehavior
import com.google.android.material.bottomsheet.BottomSheetDialog
import com.google.android.material.bottomsheet.BottomSheetDialogFragment
import com.pocketree.app.databinding.LayoutChatbotBinding

// just an overlay (does not replace the screen but sits on top of it)
class ChatbotDialogFragment: BottomSheetDialogFragment() {
    private var _binding: LayoutChatbotBinding ?= null
    private val binding get() = _binding!!

    private val messageList = mutableListOf<ChatMessage>()
    private lateinit var chatAdapter: ChatAdapter

    // access the same ViewModel used in Home/Redeem fragments
    private val sharedViewModel: UserViewModel by activityViewModels()

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = LayoutChatbotBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onCreateDialog(savedInstanceState: Bundle?): Dialog {
        val dialog = super.onCreateDialog(savedInstanceState) as BottomSheetDialog
        // This prevents the bottom sheet from being dragged down if you want it to stay open
        // dialog.behavior.isDraggable = false
        dialog.setOnShowListener { dialogInterface ->
            val bottomSheetDialog = dialogInterface as BottomSheetDialog
            val bottomSheet = bottomSheetDialog.findViewById<View>(
                com.google.android.material.R.id.design_bottom_sheet
            )

            bottomSheet?.let {
                val behavior = BottomSheetBehavior.from(it)

                // set height to 80% of screen height
                val displayMetrics = resources.displayMetrics
                val height = (displayMetrics.heightPixels * 0.8).toInt()

                it.layoutParams.height = height
                behavior.peekHeight = height
                // This forces the sheet to open at full height immediately
                dialog.behavior.state = BottomSheetBehavior.STATE_EXPANDED
                dialog.behavior.skipCollapsed = true // prevents it from getting stuck halfway
            }
        }
        return dialog
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // initialise adapter
        chatAdapter = ChatAdapter(messageList)

        // set up recycler view
        binding.chatRecyclerView.apply {
            adapter = chatAdapter
            layoutManager = LinearLayoutManager(requireContext())

            // force layout update
            setHasFixedSize(false)
        }

        // Add a welcome message
        addAIMessage("Hi! I'm PocketreeBot! Ask me a question!")

        // observe chatbot thinking state (loading indicator)
        sharedViewModel.isChatbotThinking.observe(viewLifecycleOwner) { isThinking ->
            if (isThinking) {
                showThinkingMessage()
            } else {
                // remove "Thinking..." message if it exists
                removeThinkingMessage()
            }
        }

        // observe chatbot responses from the ML model
        sharedViewModel.chatbotResponse.observe(viewLifecycleOwner) { response ->
            response?.let {
                addAIMessage(it)
                // clear response after displaying to prevent duplicate messages on rotation
                sharedViewModel.chatbotResponse.value = null
            }
        }

        binding.sendBtn.setOnClickListener {
            val userText = binding.chatbotMessage.text.toString()

            if (userText.isNotBlank()) {
                // add user message to chat
                addUserMessage(userText)
                // clear input field
                binding.chatbotMessage.text.clear()
                // send message to ML model via ViewModel
                sharedViewModel.sendChatMessage(userText)
            }
        }
    }

    private fun addUserMessage(message: String) {
        val msg = ChatMessage(message, isUser = true)
        messageList.add(msg)
        val position = messageList.size - 1
        chatAdapter.notifyItemInserted(position)
        binding.chatRecyclerView.scrollToPosition(position)
    }

    private fun addAIMessage(message: String) {
        val msg = ChatMessage(message, isUser = false)
        messageList.add(msg)
        val position = messageList.size - 1
        chatAdapter.notifyItemInserted(position)
        binding.chatRecyclerView.scrollToPosition(position)
    }

    private fun showThinkingMessage() {
        // check if thinking message already exists
        if (messageList.lastOrNull()?.text == "Thinking..." && messageList.lastOrNull()?.isUser == false) {
            return
        }

        val thinkingMsg = ChatMessage("Thinking...", false)
        messageList.add(thinkingMsg)
        val position = messageList.size-1
        chatAdapter.notifyItemInserted(position)
        binding.chatRecyclerView.scrollToPosition(position)
    }

    private fun removeThinkingMessage() {
        val thinkingIndex = messageList.indexOfLast {
            it.text == "Thinking..." && !it.isUser
        }

        if (thinkingIndex != -1) {
            messageList.removeAt(thinkingIndex)
            chatAdapter.notifyItemRemoved(thinkingIndex)
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
