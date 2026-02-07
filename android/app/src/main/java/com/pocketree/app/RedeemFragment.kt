package com.pocketree.app

import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.GridLayoutManager
import com.bumptech.glide.Glide
import com.pocketree.app.databinding.FragmentRedeemBinding

// written by Chenyu, edited by Shirley
class RedeemFragment: Fragment() {
    private var _binding: FragmentRedeemBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentRedeemBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        setupRecyclerView()
        sharedViewModel.fetchSkins()
        sharedViewModel.fetchVouchers()

        observeViewModel()
    }

    private fun observeViewModel(){
        sharedViewModel.skins.observe(viewLifecycleOwner) { skinList ->
            binding.recyclerViewSkin.adapter = RedeemAdapter(skinList) { item ->
                if (item is Skin) {
                    if (item.isRedeemed) {
                        handleRedeemedSkinClick(item) // handle equip/level check here
                    }
                    else {
                        showSkinConfirmDialog(item)
                    }
                }
            }
        }

        sharedViewModel.vouchers.observe(viewLifecycleOwner) { voucherList ->
            binding.recyclerViewVoucher.adapter = RedeemAdapter(voucherList) { item ->
                if (item is Voucher) {
                    if (!item.isRedeemed) {
                        showVoucherConfirmDialog(item)
                    }
                }
            }
        }

        // observe consolidated userState
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            // update UI using properties of state object
            binding.accountInfo.text = state.username
            binding.coinDisplay.text="${state.totalCoins} coins"

            Glide.with(requireContext())
                .load(state.profileImageUrl.ifEmpty{null}) // converts "" to null
                .circleCrop() // to make image round
                .placeholder(R.drawable.profile_pic)
                .into(binding.profilePic)
        }

        sharedViewModel.redeemSkinSuccessEvent.observe(viewLifecycleOwner) { message ->
            message?.let {
                showSuccessDialog(it) // "Skin redeemed successfully!"
                sharedViewModel.fetchSkins()
                sharedViewModel.fetchUserProfile()
                sharedViewModel.redeemSkinSuccessEvent.value = null
            }
        }

        sharedViewModel.equipSkinSuccessEvent.observe(viewLifecycleOwner) { message ->
            message?.let {
                showSuccessDialog(it) // "Skin equipped successfully!"
                sharedViewModel.fetchSkins()
                sharedViewModel.equipSkinSuccessEvent.value = null
            }
        }

        sharedViewModel.redeemVoucherSuccessEvent.observe(viewLifecycleOwner) { message ->
            message?.let {
                showSuccessDialog(it) // "Voucher used successfully!"
                sharedViewModel.fetchVouchers()
                sharedViewModel.redeemVoucherSuccessEvent.value = null
            }
        }

        sharedViewModel.errorMessage.observe(viewLifecycleOwner) { errorMsg ->
            errorMsg?.let {
                AlertDialog.Builder(requireContext())
                    .setTitle("Error")
                    .setMessage(it)
                    .setPositiveButton("OK", null)
                    .show()
                sharedViewModel.errorMessage.value = null
            }
        }
    }


    private fun setupRecyclerView() {
        // GridLayoutManager: parameter 3 indicates that 3 items are displayed in one row.
        binding.recyclerViewSkin.layoutManager = GridLayoutManager(context, 3)
        binding.recyclerViewVoucher.layoutManager = GridLayoutManager(context, 3)
    }

    private fun showSkinConfirmDialog(skin: Skin) {
        // I search that, no dialog.dismiss() required for setPositiveButton and setNegativeButton without functions
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to redeem ${skin.skinName} for ${skin.skinPrice} coins?")
            .setPositiveButton("Confirm") { _, _ ->
                processSkinRedemption(skin)
            }.setNegativeButton("Cancel", null).create().show()
    }

    private fun handleRedeemedSkinClick(skin: Skin) {
        if (skin.isEquipped) return // do nothing is already equipped

        // Requirement: All users below level 1 can only purchase, not equip.
        val currentLevel = sharedViewModel.userState.value?.currentLevelID ?: 0
        if (currentLevel >= 1) {
            AlertDialog.Builder(requireContext())
                .setTitle("Equip Skin")
                .setMessage("Do you want to equip ${skin.skinName}?")
                .setPositiveButton("Equip") {_,_ -> equipSkin(skin)}
                .setNegativeButton("Cancel", null)
                .show()
        } else {
            AlertDialog.Builder(requireContext())
                .setTitle("Not yet unlocked")
                .setMessage("You may only equip skin upon reaching the Sapling stage!\nContinue to grow your plant!")
                .setPositiveButton("OK", null)
                .create()
                .show()
        }
    }

    private fun equipSkin(skin: Skin) {
        sharedViewModel.equipSkin(skin.skinID)
    }

    private fun showVoucherConfirmDialog(voucher: Voucher) {
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to use ${voucher.voucherName}?")
            .setPositiveButton("Confirm") { _, _ ->
                redeemVoucher(voucher)
            }
            .setNegativeButton("Cancel", null)
            .create()
            .show()
    }

    private fun redeemVoucher(voucher: Voucher) {
        sharedViewModel.redeemVoucher(voucher.voucherID)
    }

    private fun processSkinRedemption(skin: Skin) {
        if (skin.isRedeemed) {
            Toast.makeText(requireContext(), "You already own this item!", Toast.LENGTH_SHORT).show()
            return
        }
        val currentCoins = sharedViewModel.userState.value?.totalCoins ?: 0
        if (currentCoins >= skin.skinPrice) {
            sharedViewModel.redeemSkin(skin.skinID)
        } else {
            AlertDialog.Builder(requireContext())
                .setTitle("Redemption Failed")
                .setMessage("Insufficient coins!")
                .setPositiveButton("Confirm", null)
                .show()
        }
    }

    private fun showSuccessDialog(message: String) {
        AlertDialog.Builder(requireContext())
            .setTitle("Redemption Successful!")
            .setMessage("$message")
            .setIcon(R.drawable.redeem)
            .setPositiveButton("OK", null)
            .create()
            .show()
    }

    override fun onDestroyView(){
        super.onDestroyView()
        _binding = null
    }
}