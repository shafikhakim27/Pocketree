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


//    // kinda just testing only - to change again when linked up with fres' model
//    private fun getAIResponse(text:String) {
//        // add a temporary "Thinking..." message
//        val thinkingMessage = ChatMessage("Thinking...", false)
//        messageList.add(thinkingMessage)
//
//        val thinkingIndex = messageList.size - 1
//        chatAdapter.notifyItemInserted(thinkingIndex)
//        binding.chatRecyclerView.scrollToPosition(thinkingIndex)
//
//        Handler(Looper.getMainLooper()).postDelayed({
//            if (!isAdded || view == null) return@postDelayed
//
//            val currentIndex = messageList.indexOf(thinkingMessage)
//            if (currentIndex != -1) {
//                messageList.removeAt(currentIndex)
//                chatAdapter.notifyItemRemoved(currentIndex)
//            }
//
//            val response = generateSmartResponse(text)
//            addAIMessage(response)
//
//        }, 1500)
//    }
//
//    private fun generateSmartResponse(input: String): String {
//        val state = sharedViewModel.userState.value
//        val text = input.lowercase()
//
//        return when {
//            text.contains("coin") || text.contains("rich") ->
//                "You have ${state?.totalCoins} coins. ${if ((state?.totalCoins ?: 0) > 100) "You're doing great!" else "Keep completing tasks to earn more!"}"
//
//            text.contains("health") || text.contains("wither") -> {
//                if (state?.isWithered == true)
//                    "Your plant has withered! Quick, complete a task to revive it!"
//                else
//                    "Your plant health is at ${state?.plantHealthPercent}%. It's looking ${if ((state?.plantHealthPercent ?: 0) > 70) "vibrant" else "a bit thirsty"}!"
//            }
//
//            text.contains("level") || text.contains("stage") ->
//                "You are currently at the ${state?.levelName} stage. Keep it up!"
//
//            text.contains("hello") || text.contains("hi") ->
//                "Hi ${state?.username}! I'm your plant assistant. How can I help you grow today?"
//
//            else -> "I'm not sure about that, but your ${state?.levelName} is looking good! Try asking about your 'coins' or 'health'."
//        }
//    }
//    // end of test

        // TODO, but for now
//        binding.chatRecyclerView.postDelayed({
//            // remove the "Thinking..." message
//            messageList.removeAt(messageList.size - 1)
//            chatAdapter.notifyItemRemoved(messageList.size)

//            // add in the real response
//            val response = sharedViewModel.generateAIResponse(userText)
//            messageList.add(ChatMessage(response, false))
//            chatAdapter.notifyItemInserted(messageList.size - 1)
//            binding.chatRecyclerView.scrollToPosition(messageList.size - 1)
//        }, 1500) // 1.5 second delay feels natural

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
