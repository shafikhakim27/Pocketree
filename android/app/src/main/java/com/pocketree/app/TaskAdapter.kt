package com.pocketree.app

import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

// this adapter handles the visual "checking off" of tasks
// if task is completed, the click listener is disabled and UI is changed

class TaskAdapter(
    private var taskList: List<Task>,
    private val onCompleteClick: (Task) -> Unit,
    private val onPassClick: (Task) -> Unit
): RecyclerView.Adapter<TaskAdapter.TaskViewHolder>() {

    fun updateTasks(newTasks:List<Task>) {
        this.taskList = newTasks
        notifyDataSetChanged() // redraw the visible views
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): TaskViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_task, parent, false)
        return TaskViewHolder(view)
    }

    override fun onBindViewHolder(holder: TaskViewHolder, position:Int) {
        val task = taskList[position]

        // bind data to the views held by ViewHolder
        holder.description.text = task.description
        holder.reward.text = "${task.coinReward} coins"

        // reset all click listeners first to avoid stale listeners
        holder.actionButton.setOnClickListener(null)
        holder.passButton.setOnClickListener(null)

        // reset visibility and state for recycled holders
        holder.actionButton.visibility = View.VISIBLE
        holder.passButton.visibility = View.VISIBLE
        holder.actionButton.isEnabled = true
        holder.passButton.isEnabled = true
        holder.itemView.alpha = 1.0f

        when {
            // task is completed
            !task.isPassed && task.isCompleted -> {
                holder.actionButton.text = "Completed!"
                holder.actionButton.isEnabled = false
                holder.passButton.visibility = View.GONE
                holder.itemView.alpha = 0.5f // visual "checked off" state
            }
            // task is passed
            task.isPassed && !task.isCompleted -> {
                holder.passButton.text = "Passed!"
                holder.passButton.isEnabled = false
                holder.actionButton.visibility = View.GONE
                holder.itemView.alpha = 0.5f
            }

            else -> {
                holder.actionButton.text =
                    if (task.requiresEvidence) "Take a photo" else "Let's go!"
                holder.passButton.text = "Pass"

                holder.actionButton.isEnabled = true
                holder.passButton.isEnabled = true

                holder.passButton.setOnClickListener {
                    onPassClick(task)
                }

                holder.actionButton.setOnClickListener {
                    onCompleteClick(task)
                }
            }
        }
    }

    override fun getItemCount() = taskList.size

    class TaskViewHolder(itemView: View): RecyclerView.ViewHolder(itemView) {
        val description: TextView = itemView.findViewById(R.id.descriptionTv)
        val reward: TextView = itemView.findViewById(R.id.rewardTv)
        val actionButton: Button = itemView.findViewById(R.id.actionBtn)
        val passButton: Button = itemView.findViewById(R.id.passBtn)
    }
}