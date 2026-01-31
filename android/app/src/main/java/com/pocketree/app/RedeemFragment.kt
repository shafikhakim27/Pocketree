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
import com.pocketree.app.databinding.FragmentRedeemBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import com.pocketree.app.databinding.ItemBadgeBinding
import com.pocketree.app.databinding.ItemRedeemBinding

class RedeemFragment: Fragment() {
    private var _binding: FragmentRedeemBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
    private val client = NetworkClient.okHttpClient
    private val baseUrl = "http://10.0.2.2:5042/api/Task"


    // mock data for now
    private val skinList = listOf(
        Skin(1, "Item1", 10, R.drawable.redeem_item_1, true, true),
        Skin(2, "Item2", 20, R.drawable.redeem_item_2, true, false),
        Skin(3, "Item3", 30, R.drawable.redeem_item_3, false, false)
    )
    private val voucherList = listOf(
        Voucher(4, "Voucher 1", "none", true, true),
        Voucher(5, "Voucher 2", "none", true, false),
        Voucher(6, "Voucher 3", "none", false, false)
    )


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

        // observe consolidated userState
        sharedViewModel.userState.observe(viewLifecycleOwner) { state ->
            // update UI using properties of state object
            binding.accountInfo.text = state.username
            binding.coinDisplay.text="${state.totalCoins} coins"
        }

        sharedViewModel.redeemSuccessEvent.observe(viewLifecycleOwner) { message ->
            message?.let {
                showSuccessDialog(it) // message: "Skin redeemed successfully!"
                sharedViewModel.redeemSuccessEvent.value = null
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
        binding.recyclerViewSkin.adapter = RedeemAdapter(skinList) { item ->
            if (item is Skin) {
                if (!item.isRedeemed) {
                    showSkinConfirmDialog(item)
                } else {
                    handleRedeemedSkinClick(item)  // If you have already purchased it, proceed to the new level check logic.
                }
            }
        }

        binding.recyclerViewVoucher.layoutManager = GridLayoutManager(context, 3)
        binding.recyclerViewVoucher.adapter = RedeemAdapter(voucherList) { item ->
            if (item is Voucher) {
                if (item.isValid) {
                    showVoucherConfirmDialog(item)
                }
            }
        }
    }


    private fun showSkinConfirmDialog(skin: Skin) {
        // I search that, no dialog.dismiss() required for setPositiveButton and setNegativeButton without functions
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to redeem ${skin.skinName} for ${skin.skinPrice} coins?")
            .setPositiveButton("Confirm") { _, _ ->
                preRedeem(skin)
            }
            .setNegativeButton("Cancel", null)
            .create()
            .show()
    }


    private fun handleRedeemedSkinClick(skin: Skin) {
        // Requirement: All users below level 1 can only purchase, not equip.
        val currentLevel = sharedViewModel.userState.value?.currentLevelID ?: 0
        if (currentLevel >= 1) {
            // TODO: equipSkin(skin)
        } else {
            AlertDialog.Builder(requireContext())
                .setTitle("Skin features are not yet unlocked")
                .setMessage("Level 1 required")
                .setPositiveButton("OK", null)
                .create()
                .show()
        }
    }


    private fun showVoucherConfirmDialog(voucher: Voucher) {
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to use ${voucher.voucherName}?")
            .setPositiveButton("Confirm") { dialog, _ ->
                //TODO: useVoucher(voucher)
                dialog.dismiss()
            }
            .setNegativeButton("Cancel", null)
            .create()
            .show()
    }


    private fun preRedeem(skin: Skin) {
        if (skin.isRedeemed) {
            Toast.makeText(requireContext(), "You already own this item!", Toast.LENGTH_SHORT).show()
            return
        }

        val currentCoins = sharedViewModel.userState.value?.totalCoins ?: 0
        if (currentCoins >= skin.skinPrice) {
            processRedemption(skin)
        } else {
            AlertDialog.Builder(requireContext())
                .setTitle("Redemption Failed")
                .setMessage("Insufficient coins!")
                .setPositiveButton("Confirm", null)
                .show()
        }
    }


    private fun processRedemption(skin: Skin){
        // deductCoins(currentCoins - skin.skinPrice, skin) - will have error
        // showSuccessDialog(skin.skinName) - move to observer in onViewCreated()
        sharedViewModel.redeemSkin(skin.skinID)
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


    // pending equipping of skins
    // pending vouchers
}