package com.pocketree.app

import android.R.attr.level
import android.os.Bundle
import android.util.Log
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
        sharedViewModel.fetchDailyTasks()

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

//        // Initialize the list
//        val taskAdapter = TaskAdapter(emptyList()) { clickedTask ->
//            // Handle what happens when a task is clicked
//        }
//
//        // Attach the adapter to the ID we just added to the XML
//        binding.recyclerViewTasks.layoutManager = LinearLayoutManager(requireContext())
//        binding.recyclerViewTasks.adapter = taskAdapter
//
//        // Watch the "bucket" of tasks in the ViewModel
//        sharedViewModel.tasks.observe(viewLifecycleOwner) { taskList ->
//                taskAdapter.updateTasks(taskList)
//        }
//    }
        //1. Setup the Adapter
        val adapter = TaskAdapter(emptyList()) { task ->
            // This is what happens when a user clicks a task
            // We can handle navigation here later
        }

        // 2. Setup the RecyclerView
        // (Make sure 'recyclerViewTasks' is the ID in your fragment_home.xml)
        binding.recyclerViewTasks.layoutManager = LinearLayoutManager(requireContext())
        binding.recyclerViewTasks.adapter = adapter

        // 3. The "Flow": Observe the tasks and push them to the adapter
        sharedViewModel.tasks.observe(viewLifecycleOwner) { taskList ->
            if (taskList != null) {
                adapter.updateTasks(taskList)
                Log.d("HOME_FRAGMENT", "Tasks received: ${taskList.size}")
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
    }}

