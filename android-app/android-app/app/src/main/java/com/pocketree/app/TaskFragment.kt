package com.pocketree.app

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
import com.pocketree.app.databinding.FragmentTaskBinding
import java.io.ByteArrayOutputStream

class TaskFragment: Fragment() {
    private var _binding: FragmentTaskBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
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
            it.compress(Bitmap.CompressFormat.JPEG, 90, stream)
            // converting the image taken into data (90 to balance quality and upload speed)
            currentProcessingTaskId?.let{ id ->
                sharedViewModel.submitTaskWithImage(id, stream.toByteArray())
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

        sharedViewModel.username.observe(viewLifecycleOwner) { name ->
            binding.accountInfo.text = "${name ?: "User"}"
        }

        // observe coins to update coinDisplay TextView
        sharedViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins coins"
        }

        // observe the ViewModel and update the adapter when the list changes
        sharedViewModel.tasks.observe(viewLifecycleOwner) { tasks ->
            adapter.updateTasks(tasks)

            if (tasks.isNotEmpty() && tasks.all { it.isCompleted }) {
                binding.dailyStatusTv.text = "Daily tasks complete! Good job!"
                binding.dailyStatusTv.visibility = View.VISIBLE
            } else {
                binding.dailyStatusTv.visibility = View.GONE
            }
        }

        sharedViewModel.isLoading.observe(viewLifecycleOwner){ loading ->
            binding.loadingOverlay.visibility = if (loading) View.VISIBLE else View.GONE
        }

        sharedViewModel.levelUpEvent.observe(viewLifecycleOwner) { levelUp ->
            if (levelUp == true) {
                val (levelName, badgeName, voucherName) = sharedViewModel.getLevelDetails()
                AlertDialog.Builder(requireContext())
                    // returns non-null Context associated with fragment's current host (activity)
                    .setTitle("Level Up!")
                    .setMessage("Good job! You have progressed to the $levelName stage!")
                    .setPositiveButton("Yay!") { _, _ ->

                        // chain to badge dialog
                        AlertDialog.Builder(requireContext())
                            .setTitle("Badge Obtained!")
                            .setMessage("You have earned the $badgeName badge! View it under the Home tab.")
                            .setPositiveButton("Got it!") { _, _ ->

                                AlertDialog.Builder(requireContext())
                                    .setTitle("Voucher Obtained!")
                                    .setMessage("You have earned a voucher! Check the Redeeem tab.")
                                    .setPositiveButton("Awesome!", null)
                                    .show()
                            }.show()
                    }.show()

                sharedViewModel.levelUpEvent.value = false
                // reset the event so the notice doesn't fire again
            }
        }

        sharedViewModel.errorMessage.observe(viewLifecycleOwner) { message ->
            message?.let {
                Toast.makeText(requireContext(), it, Toast.LENGTH_SHORT).show()
                sharedViewModel.errorMessage.value = null // clear message after showing
            }
        }
    }

    private fun onTaskClick(task: Task) {

        if (task.requiresEvidence) {
            currentProcessingTaskId = task.taskID
            getPhoto.launch(null) // opens camera
        } else {
            // no photo needed, just send completion into backend
            sharedViewModel.completeTaskDirectly(task.taskID)
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}

