package com.pocketree.app

import android.os.Bundle
import android.view.View
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import com.pocketree.app.databinding.FragmentHomeBinding

class HomeFragment: Fragment() {
    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!

    // ViewModel (to get the data)
    private val sharedViewModel: UserViewModel by activityViewModels()

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        _binding = FragmentHomeBinding.bind(view)

        sharedViewModel.username.observe(viewLifecycleOwner) { name ->
            binding.accountInfo.text = "Hello, $name"
        }

        sharedViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins pts"
        }

        // create withering logic also - reminder one day before
        sharedViewModel.isWithered.observe(viewLifecycleOwner) { withered ->
            if (withered) {
                binding.statusWarning.text = "Your plant has withered."
                binding.statusWarning.visibility = View.VISIBLE
                // remove below if changing to picture of dying tree
                binding.plant.visibility = View.GONE // make the plant look "faded"
            } else {
                binding.statusWarning.visibility = View.GONE
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
}
