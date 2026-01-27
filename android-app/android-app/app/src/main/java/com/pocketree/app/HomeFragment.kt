package com.pocketree.app

import android.R.attr.level
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import com.pocketree.app.databinding.FragmentHomeBinding

class HomeFragment: Fragment() {
    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!

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

        // test
        binding.plant.setImageResource(R.drawable.mighty_oak)

        sharedViewModel.username.observe(viewLifecycleOwner) { name ->
            binding.accountInfo.text = "${name ?: "User"}"
        }

        sharedViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins coins"
        }

        // kiv for image insertion
        // Observing the Image URL (Use Glide or Picasso to load it)
        sharedViewModel.levelImageUrl.observe(viewLifecycleOwner) { url ->
            if (!url.isNullOrEmpty()) {
                // Example using Glide: Glide.with(this).load(url).into(binding.plant)
            }
        }

        sharedViewModel.levelName.observe(viewLifecycleOwner) { level ->
            binding.levelDisplay.text = "Current Stage: ${level ?: "Seedling"}"
        }

        // create withering logic also - reminder one day before
        sharedViewModel.isWithered.observe(viewLifecycleOwner) { withered ->
            if (withered) {
                binding.statusWarning.text = "Your plant has withered."
                binding.statusWarning.visibility = View.VISIBLE
                // remove below if changing to picture of dying tree
                binding.plant.alpha = 0.3f // make the plant look "faded"
            } else {
                binding.statusWarning.visibility = View.GONE
                binding.plant.alpha = 1.0f
                binding.plant.visibility = View.VISIBLE
            }
        }
    }

    // kiv for now - insertion of plant images
    // viewModel.levelImageURL.observe(this) { url ->
    //    // Use a library like Glide or Picasso to load the image from the URL
    //    // Note: Since your API returns "~/images/...", you may need to
    //    // clean the URL to match your server's public address.
    //    val fullUrl = url.replace("~/", baseUrl + "/")
    //    Glide.with(this).load(fullUrl).into(binding.plantImageView)
    //}

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }

    // insert badge photos below the tree?
}
