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
import com.bumptech.glide.Glide
import com.pocketree.app.databinding.FragmentTaskBinding
import java.io.ByteArrayOutputStream

class TaskFragment: Fragment() {
    private var _binding: FragmentTaskBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
    private var currentProcessingTaskId: Int? = null
    private lateinit var taskAdapter: TaskAdapter

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentTaskBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState:Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // initialising task adapter and recycler view
        taskAdapter = TaskAdapter(
            emptyList(),
            onCompleteClick = {task -> onTaskCompleteClick(task)},
            onPassClick = {task -> onTaskPassClick(task)}
        )
        binding.recyclerViewTasks.layoutManager = LinearLayoutManager(context)
        binding.recyclerViewTasks.adapter = taskAdapter

        // start observers (update UI if anything in View Model changes)
        observeViewModel()
    }

    private fun observeViewModel() {
        // observe consolidated state object
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            state?.let {
                binding.accountInfo.text = state.username
                binding.coinDisplay.text = "${state.totalCoins} coins"

                Glide.with(requireContext())
                    .load(state.profileImageUrl.ifEmpty{null}) // converts "" to null
                    .circleCrop() // to make image round
                    .placeholder(R.drawable.profile_pic)
                    .error(R.drawable.profile_pic)
                    .into(binding.profilePic)
            }
        }

        // observe tasks list
        sharedViewModel.tasks.observe(viewLifecycleOwner) { tasks ->
            if (tasks == null || _binding == null) return@observe

            taskAdapter.updateTasks(tasks)

            if (tasks.isNotEmpty() && tasks.all { it.isCompleted || it.isPassed }) {
                binding.dailyStatusTv.text = "Come back tomorrow for new tasks!"
                binding.dailyStatusTv.visibility = View.VISIBLE
            } else {
                binding.dailyStatusTv.visibility = View.GONE
            }
        }

        sharedViewModel.isLoading.observe(viewLifecycleOwner) { loading ->
            binding.loadingOverlay.visibility = if (loading) View.VISIBLE else View.GONE
        }

        sharedViewModel.levelUpEvent.observe(viewLifecycleOwner) { details ->
            details?.let { (levelName, badgeName, voucherName) ->
                showLevelUpDialog(levelName, badgeName, voucherName)
                sharedViewModel.levelUpEvent.value = null
            }
        }
    }

    private fun showLevelUpDialog(levelName: String, badgeName:String, voucherName: String) {
        AlertDialog.Builder(requireContext())
            // returns non-null Context associated with fragment's current host (activity)
            .setTitle("Level Up!")
            .setMessage("Good job! You have progressed to the $levelName stage!")
            .setPositiveButton("Yay!") { dialog, _ ->
                dialog.dismiss()

                // show badge dialog next
                if (badgeName.isNotEmpty()) {
                    showBadgeDialog(badgeName, voucherName)
                } else if (voucherName.isNotEmpty()) {
                    showVoucherDialog(voucherName)
                }
            }
            .setCancelable(false)
            .show()
    }

    private fun showBadgeDialog(badgeName: String, voucherName: String) {
        AlertDialog.Builder(requireContext())
            .setTitle("Badge Obtained!")
            .setMessage("You have earned the $badgeName badge! View it under the Home tab.")
            .setPositiveButton("Got it!") { dialog, _ ->
                dialog.dismiss()

                if (voucherName.isNotEmpty()) {
                    showVoucherDialog(voucherName)
                }
            }
            .setCancelable(false)
            .show()
    }

    private fun showVoucherDialog(voucherName:String) {
        AlertDialog.Builder(requireContext())
            .setTitle("Voucher Obtained!")
            .setMessage("You have earned $voucherName! Check the Redeeem tab.")
            .setPositiveButton("Awesome!") { dialog, _ ->
                dialog.dismiss()
            }
            .show()
    }

    // for the "Let's Go!"/"Take a photo" button
    private fun onTaskCompleteClick(task: Task) {
        // safety check - do nothing if task is already completed/passed
        if (task.isCompleted || task.isPassed) {
            return
        }

        if (task.requiresEvidence) {
            currentProcessingTaskId = task.taskID
            getPhoto.launch(null) // opens camera
        } else {
            // no photo needed, just send completion into backend
            sharedViewModel.submitTask(task.taskID, "Completed")
        }
    }

    // to handle "Pass" button clicks
    private fun onTaskPassClick(task: Task) {
        if (task.isCompleted || task.isPassed) {
            return
        }
        sharedViewModel.submitTask(task.taskID, "Passed")
    }

    private val getPhoto = registerForActivityResult(
        ActivityResultContracts
            .TakePicturePreview()
    ) { bitmap ->
        if (bitmap == null) {
            // user cancelled camera
            currentProcessingTaskId = null
            return@registerForActivityResult
        }

        try {
            // camera returns a bitmap if photo is taken
            val stream = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.JPEG, 90, stream)
            // converting the image taken into data (90 to balance quality and upload speed)
            val imageBytes = stream.toByteArray()

            currentProcessingTaskId?.let { id ->
                sharedViewModel.submitTask(id, "Completed", imageBytes)
                // this part checks if there is an active task ID
                // and converts that memory stream into a Byte Array and tells userViewModel to upload it
                currentProcessingTaskId = null
            } ?: run {
                Toast.makeText(requireContext(),
                    "Error: Task ID missing",
                    Toast.LENGTH_SHORT
                ).show()
            }
        } catch (e:Exception) {
            Toast.makeText(requireContext(),
                "Error processing photo",
                Toast.LENGTH_SHORT
            ).show()
            currentProcessingTaskId = null
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}