package com.pocketree.app

import android.animation.ObjectAnimator
import android.app.AlertDialog
import android.content.res.ColorStateList
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.LinearLayoutManager
import com.bumptech.glide.Glide
import com.pocketree.app.databinding.FragmentHomeBinding

class HomeFragment: Fragment() {
    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!
    var wasWithered:Boolean? = null

    // ViewModel (to get the data)
    private val sharedViewModel: UserViewModel by activityViewModels()
    private val badgeAdapter = BadgeAdapter(emptyList())

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentHomeBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // set up recyclerview layout manager for badges
        binding.recyclerViewBadges.adapter = badgeAdapter
        binding.recyclerViewBadges.layoutManager= LinearLayoutManager(requireContext(),
            LinearLayoutManager.HORIZONTAL, false)

        observeViewModel()

        sharedViewModel.fetchLatestBadges() // fetch data
        sharedViewModel.fetchUserProfile()
//
//        // Mock data for testing withered stage
//        view.postDelayed({
//            sharedViewModel.mockWitheredState() // trigger withering
//        }, 3000)
//
//        view.postDelayed({
//            sharedViewModel.mockReviveState() // trigger revive
//        }, 6000)

//        // --- Mock testing for healthBar ---
//
//        view.postDelayed({
//            sharedViewModel.setMockState(percent = 100, withered = false, stage = "Healthy Tree")
//        }, 2000)
//
//        view.postDelayed({
//            sharedViewModel.setMockState(percent = 60, withered = false, stage = "Need Water")
//        }, 5000)
//
//        view.postDelayed({
//            sharedViewModel.setMockState(percent = 30, withered = false, stage = "Dying...")
//        }, 8000)
//
//        view.postDelayed({
//            sharedViewModel.setMockState(percent = 0, withered = true, stage = "Withered")
//        }, 11000)
//
//        // --- Mock testing ends ---
    }

    fun observeViewModel() {
        // observe consolidated state object
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            binding.accountInfo.text = state.username
            binding.coinDisplay.text = "${state.totalCoins} coins"
            binding.healthBar.progress = state.plantHealthPercent
            binding.levelDisplay.text = "Current Stage: ${state.levelName}"

            Glide.with(this@HomeFragment)
                .load(state.levelImageUrl)
                //.placeholder(R.drawable.tree_seedling)
                .error(R.drawable.tree_seedling)
                .into(binding.plant)

            Glide.with(requireContext())
                .load(state.profileImageUrl.ifEmpty{null}) // converts "" to null
                .circleCrop() // to make image round
                .error(R.drawable.profile_pic)
                .into(binding.profilePic)

            // to create "health bar" for plant (based on number of inactive days)
            ObjectAnimator.ofInt(binding.healthBar, "progress", state.plantHealthPercent)
                .setDuration(1000) // takes 1 sec to complete "animation" of change in bar color
                .start()

            // color situations
            val colorResId = when {
                state.isWithered || state.plantHealthPercent == 0 -> {
                    // withered
                    R.color.grey
                }
                state.plantHealthPercent < 40 -> {
                    // 3 days no activity
                    R.color.red
                }
                state.plantHealthPercent < 65 -> {
                    // 2 days no activity
                    R.color.yellow
                }
                else -> {
                    // i day no activity
                    R.color.green
                }
            }

            // implement to change colour of health bar
            val colorValue = ContextCompat.getColor(requireContext(), colorResId)
            binding.healthBar.progressTintList = ColorStateList.valueOf(colorValue)

            handleWithering(state.isWithered)
        }

        sharedViewModel.earnedBadges.observe(viewLifecycleOwner) { badges ->
            if (!badges.isNullOrEmpty()) {
                binding.badgesHeader.visibility = View.VISIBLE
                badgeAdapter.updateData(badges)
            } else {
                binding.badgesHeader.visibility = View.GONE
            }
        }
    }

    // helper function to handle withering logic
    private fun handleWithering(withered: Boolean) {
        // if withered tree has revived
        if (wasWithered == true && !withered) {
            AlertDialog.Builder(requireContext())
                .setTitle("Plant Revived")
                .setMessage("Your plant has revived!\nTake good care of it!")
                .setPositiveButton("Yay!", null)
                .show()
        }
        if (withered) {
            binding.statusWarning.text = "Your plant has withered.\nComplete a task to revive it!"
            binding.statusWarning.visibility = View.VISIBLE
            // remove below if changing to picture of dying tree
            binding.plant.alpha = 0.3f // make the plant look "faded"
        } else {
            binding.statusWarning.visibility = View.GONE
            binding.plant.alpha = 1.0f
            binding.plant.visibility = View.VISIBLE
        }

        // update tracker for the next change
        wasWithered = withered
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
