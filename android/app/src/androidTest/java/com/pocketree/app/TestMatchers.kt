package com.pocketree.app

import android.view.View
import androidx.test.espresso.matcher.BoundedMatcher
import com.google.android.material.textfield.TextInputLayout
import org.hamcrest.Description
import org.hamcrest.Matcher

fun hasTextInputLayoutErrorText(expectedError: String): Matcher<View> {
    return object : BoundedMatcher<View, TextInputLayout>(TextInputLayout::class.java) {
        override fun describeTo(description: Description) {
            description.appendText("with TextInputLayout error: $expectedError")
        }

        override fun matchesSafely(item: TextInputLayout): Boolean {
            return expectedError == item.error?.toString()
        }
    }
}
