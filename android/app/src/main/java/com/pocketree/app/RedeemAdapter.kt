package com.pocketree.app

import android.graphics.Color
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.RecyclerView
import com.bumptech.glide.Glide
import com.pocketree.app.databinding.ItemRedeemBinding

// written by Chenyu, edited by Shirley
// Change <Skin> to <Any> for both skins and vouchers
class RedeemAdapter(
    private var items: List<Any>,
    private val onItemClick: (Any) -> Unit
) : RecyclerView.Adapter<RedeemAdapter.RedeemViewHolder>() {

    fun updateData(newList:List<Any>) {
        this.items = newList
        notifyDataSetChanged()
    }

    class RedeemViewHolder(val binding: ItemRedeemBinding) : RecyclerView.ViewHolder(binding.root)

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RedeemViewHolder {
        val binding = ItemRedeemBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return RedeemViewHolder(binding)
    }

    override fun onBindViewHolder(holder: RedeemViewHolder, position: Int) {
        val item = items[position]
        val context = holder.itemView.context

        holder.binding.itemPrice.visibility = View.VISIBLE
        holder.binding.itemStatus.visibility = View.GONE
        holder.binding.root.setOnClickListener(null)  // Global processing: Clear click events
        holder.binding.root.alpha = 1.0f

        when (item) {
            is Skin -> {
                holder.binding.itemName.text = item.skinName

                // image logic
                val safeName = item.skinName?.lowercase()?.replace(" ","_") ?: ""
                val imageName = if (safeName.isNotEmpty()) "redeem_skin_${safeName}" else ""
                val resourceId = if (imageName.isNotEmpty()) {
                    context.resources.getIdentifier(imageName, "drawable", context.packageName)
                } else 0

                val imageSource = when {
                    !item.imageURL.isNullOrEmpty() -> item.imageURL
                    resourceId != 0 -> resourceId
                    else -> R.drawable.redeem_item_1
                }

                Glide.with(context)
                    .load(imageSource)
                    //.placeholder(R.drawable.redeem_item_1)
                    .error(R.drawable.redeem_item_1)
                    .into(holder.binding.itemImage)

                if (!item.isRedeemed) {
                    holder.binding.itemPrice.text = "${item.skinPrice} coins"
                    holder.binding.itemPrice.setTextColor(
                        ContextCompat.getColor(
                            context,
                            R.color.green
                        )
                    )
                    holder.binding.root.setOnClickListener { onItemClick(item) }
                } else {
                    holder.binding.itemStatus.visibility = View.VISIBLE
                    if (item.isEquipped) {
                        holder.binding.itemPrice.visibility = View.GONE
                        holder.binding.itemStatus.text = "Equipped"
                        holder.binding.itemStatus.setTextColor(
                            ContextCompat.getColor(
                                context,
                                R.color.blue
                            )
                        )
                    } else {
                        holder.binding.itemPrice.text="Redeemed"
                        holder.binding.itemStatus.text = "Click to Equip"
                        holder.binding.itemStatus.setTextColor(
                            ContextCompat.getColor(
                                context,
                                R.color.blue
                            )
                        )
                        holder.binding.root.setOnClickListener { onItemClick(item) }
                    }
                }
            }
            is Voucher -> {
                Glide.with(context).clear(holder.binding.itemImage)
                // prevents any skin images from suddenly appearing over the voucher icon
                // in the event a user scrolls the page quickly and the skin image is already loaded
                holder.binding.itemImage.setImageResource(R.drawable.redeem_voucher)
                holder.binding.itemName.text = item.voucherName
                holder.binding.itemPrice.visibility = View.GONE
                holder.binding.itemStatus.visibility = View.VISIBLE

                if (!item.isRedeemed) {
                    holder.binding.itemStatus.text = "Usable"
                    holder.binding.itemStatus.setTextColor(
                        ContextCompat.getColor(
                            context,
                            R.color.green
                        )
                    )
                    holder.binding.root.setOnClickListener { onItemClick(item) }
                } else {
                    holder.binding.itemStatus.text = "Used"
                    holder.binding.itemStatus.setTextColor(Color.GRAY)
                    holder.binding.root.alpha = 0.5f
                }
            }
        }
    }
    override fun getItemCount() = items.size
}