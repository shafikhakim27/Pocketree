package com.pocketree.app

import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.LinearLayoutManager
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

        // test - to remove later on
        binding.plant.setImageResource(R.drawable.mighty_oak)

        // set up recyclerview layout manager for badges
        binding.recyclerViewBadges.layoutManager = LinearLayoutManager(requireContext(),
            LinearLayoutManager.HORIZONTAL, false)

        sharedViewModel.fetchEarnedBadges() // fetch data
        observeViewModel()
    }

    // kiv for now - insertion of plant images
    // viewModel.levelImageURL.observe(this) { url ->
    //    // Use a library like Glide or Picasso to load the image from the URL
    //    // Note: Since your API returns "~/images/...", you may need to
    //    // clean the URL to match your server's public address.
    //    val fullUrl = url.replace("~/", baseUrl + "/")
    //    Glide.with(this).load(fullUrl).into(binding.plantImageView)
    //}

    fun observeViewModel() {
        // observe consolidated state object
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            binding.accountInfo.text = state.username
            binding.coinDisplay.text = "${state.totalCoins} coins"

            binding.levelDisplay.text = "Current Stage: ${state.levelName}"

            // KIV!!!!
            // update plant image
            if (state.levelImageUrl.isNotEmpty()) {
                // val fullUrl = state.levelImageUrl.replace("~/", baseUrl + "/")
                // Glide.with(this).load(fullUrl).into(binding.plant)
            }

            handleWithering(state.isWithered)

            sharedViewModel.earnedBadges.observe(viewLifecycleOwner) { badges ->
                if (badges != null) {
                    binding.badgesHeader.visibility = View.VISIBLE
                    binding.recyclerViewBadges.adapter = BadgeAdapter(badges)
                }
            }
        }
    }

    // helper function to handle withering logic
    private fun handleWithering(withered: Boolean) {
        // if withered tree has revived
        if (wasWithered == true && !withered) {
            AlertDialog.Builder(requireContext())
                .setTitle("Plant Revived")
                .setMessage("Your plant has revived! Good job!")
                .setPositiveButton("Yay!", null)
                .show()
        }
        if (withered) {
            binding.statusWarning.text = "Your plant has withered. Complete a task to revive it!"
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

    // need to work on getting the tree images up (finish Redeem portion first)
}
