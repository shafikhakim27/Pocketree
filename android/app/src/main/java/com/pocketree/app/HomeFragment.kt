package com.pocketree.app

import android.animation.ObjectAnimator
import android.app.AlertDialog
import android.content.res.ColorStateList
import android.graphics.Color
import android.os.Bundle
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
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
        binding.recyclerViewBadges.layoutManager = LinearLayoutManager(requireContext(),
            LinearLayoutManager.HORIZONTAL, false)

        sharedViewModel.fetchLatestBadges() // fetch data
        observeViewModel()
        sharedViewModel.fetchUserProfile()
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
                .placeholder(R.drawable.mighty_oak)
                .into(binding.plant)

            Glide.with(requireContext())
                .load(state.profileImageUrl.ifEmpty{null}) // converts "" to null
                .circleCrop() // to make image round
                .placeholder(R.drawable.profile_pic)
                .into(binding.profilePic)

            // to create "health bar" for plant (based on number of inactive days)
            ObjectAnimator.ofInt(binding.healthBar, "progress", state.plantHealthPercent)
                .setDuration(1000) // takes 1 sec to complete "animation" of change in bar color
                .start()

            // colour of health bar changes to red when percentage drops below 40%
            // (ie user has not done a task in 2 days and tree will wither in another day)
            when {
                state.plantHealthPercent == 0 || state.isWithered -> {
                    // withered
                    binding.healthBar.progressTintList = ColorStateList.valueOf(Color.DKGRAY)
                }
                state.plantHealthPercent < 40 -> {
                    binding.healthBar.progressTintList = ColorStateList.valueOf(Color.RED)
                }
                else -> {
                    binding.healthBar.progressTintList = ColorStateList.valueOf(Color.parseColor("#4CAF50"))
                }
            }
            handleWithering(state.isWithered)
        }

        sharedViewModel.earnedBadges.observe(viewLifecycleOwner) { badges ->
            if (!badges.isNullOrEmpty()) {
                binding.badgesHeader.visibility = View.VISIBLE
                binding.recyclerViewBadges.adapter = BadgeAdapter(badges)
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
