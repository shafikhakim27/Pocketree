package com.pocketree.app

import android.R.attr.bitmap
import android.R.attr.password
import android.app.AlertDialog
import android.graphics.Bitmap
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.LinearLayoutManager
import com.pocketree.app.Task
import com.pocketree.app.databinding.FragmentTaskBinding
import okhttp3.Call
import okhttp3.Callback
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.IOException
import kotlin.math.PI

class TaskFragment: Fragment() {
    private var _binding: FragmentTaskBinding? = null
    private val binding get() = _binding!!

    private val userViewModel: UserViewModel by activityViewModels()
    private var currentProcessingTaskId: Int? = null

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentTaskBinding.inflate(inflater, container, false)
        return binding.root
    }

    private val getPhoto = registerForActivityResult(ActivityResultContracts
        .TakePicturePreview()) { bitmap -> bitmap?.let { // camera returns a bitmap if photo is taken
            val stream = ByteArrayOutputStream()
            it.compress(Bitmap.CompressFormat.JPEG, 100, stream)
            // converting the image taken into data (100 means max quality)
            currentProcessingTaskId?.let{ id ->
                userViewModel.submitTaskWithImage(id, stream.toByteArray())
                // this part checks if there is an active task ID
                // and converts that memory stream into a Byte Array and tells userViewModel to upload it
            }
        }
    }

    override fun onViewCreated(view: View, savedInstanceState:Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // whenever the list changes in ViewModel, update UI
        // initialising task adapter and recycler view
        val adapter = TaskAdapter(emptyList()) { task -> onTaskClick(task) }
        binding.recyclerViewTasks.layoutManager = LinearLayoutManager(context)
        binding.recyclerViewTasks.adapter = adapter

        // observe the ViewModel and update the adapter when the list changes
        userViewModel.tasks.observe(viewLifecycleOwner) { tasks ->
            adapter.updateTasks(tasks)

            if (tasks.isNotEmpty() && tasks.all { it.isCompleted }) {
                binding.dailyStatusTv.text = "Daily tasks complete! Good job!"
                binding.dailyStatusTv.visibility = View.VISIBLE
            } else {
                binding.dailyStatusTv.visibility = View.GONE
            }
        }

        // observe coins to update coinDisplay TextView
        userViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins coins"
        }

        userViewModel.isLoading.observe(viewLifecycleOwner){ loading ->
            binding.loadingOverlay.visibility = if (loading) View.VISIBLE else View.GONE
        }

        userViewModel.levelUpEvent.observe(viewLifecycleOwner) { levelUp ->
            if (levelUp == true) {
                val currentLevelName = userViewModel.levelName.value ?: "next"
                AlertDialog.Builder(requireContext())
                    // returns non-null Context associated with fragment's current host (activity)
                    .setTitle("Level Up!")
                    .setMessage("Good job! You have progressed to the $currentLevelName stage!")
                    .setPositiveButton("Yay!", null) // button for dismissal of notification
                    .show()

                userViewModel.levelUpEvent.value = false
            // reset the event so the notice doesn't fire again
            }
        }

        userViewModel.errorMessage.observe(viewLifecycleOwner) { message ->
            message?.let {
                Toast.makeText(requireContext(), it, Toast.LENGTH_SHORT).show()
                userViewModel.errorMessage.value = null // clear message after showing
            }
        }
    }

    private fun onTaskClick(task: Task) {
        val userId = 1 // logic to get the current user ID

        if (task.requiresEvidence) {
            currentProcessingTaskId = task.taskID
            getPhoto.launch(null) // opens camera
        } else {
            // no photo needed, just send completion into backend
            userViewModel.completeTaskDirectly(task.taskID)
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}

